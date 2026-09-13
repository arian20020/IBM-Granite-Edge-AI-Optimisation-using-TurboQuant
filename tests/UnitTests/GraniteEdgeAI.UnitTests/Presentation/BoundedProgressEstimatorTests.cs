using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Presentation;

[TestClass]
public sealed class BoundedProgressEstimatorTests
{
    [TestMethod]
    [TestCategory("ProgressCompletion")]
    public void SuccessfulRowCatchesUpWithinFixedDeadlineWithoutDelayingNextRow()
    {
        var clock = new ManualClock();
        object completion = Create(clock, "BoundedProgressCompletion");
        object owner = new();
        Call(completion, "Observe", owner, true, false, .1, true);
        Call(completion, "Observe", owner, false, true, 1d, true);
        Assert.AreEqual(.1, Value(completion));
        for (int tick = 1; tick <= 5; tick++)
        {
            clock.Advance(.1);
            Call(completion, "Observe", owner, false, true, 1d, true);
            if (tick < 5) Assert.IsTrue(Value(completion) > .1 && Value(completion) < 1);
        }
        Assert.AreEqual(1d, Value(completion));
        Assert.IsFalse((bool)Call(completion, "IsCompleting")!);
        object[] frame = [0d];
        Assert.IsTrue((bool)Call(completion, "TryGetCompletionFrame", frame)!);
        Assert.AreEqual(1d, (double)frame[0], "Render 100% once before replacing it with Complete.");
        Assert.IsFalse((bool)Call(completion, "TryGetCompletionFrame", frame)!);
        Call(completion, "Observe", new object(), false, true, 1d, true);
        Assert.AreEqual(1d, Value(completion), "Initially complete snapshots must not invent work.");
        owner = new();
        Call(completion, "Observe", owner, true, false, .1, true);
        Call(completion, "Observe", owner, false, true, 1d, false);
        Assert.AreEqual(1d, Value(completion), "Reduced motion snaps.");
        owner = new();
        Call(completion, "Observe", owner, true, false, .1, true);
        Call(completion, "Observe", owner, false, false, .1, true);
        clock.Advance(1);
        Assert.IsFalse((bool)Call(completion, "IsCompleting")!);
        Assert.IsTrue(Value(completion) < 1, "Cancellation/failure is not successful completion.");
        Call(completion, "Reset");
        clock.Advance(1);
        Assert.AreEqual(0d, Value(completion));
    }
    [TestMethod]
    public void ContinuousRetargetingAdvancesBeforeRebasingAndCatchesStageJump()
    {
        var clock = new ManualClock();
        object interpolation = Create(clock, "BoundedProgressInterpolation");
        for (int step = 1; step <= 20; step++)
        {
            clock.Advance(.05);
            Call(interpolation, "SetTarget", step / 20d, true);
        }
        Assert.IsTrue(Value(interpolation) > .7, "Continuous retargets cannot reset progress to zero each tick.");
        clock.Advance(.18); // Finish that transition before testing a fresh authoritative stage jump.
        Call(interpolation, "SetTarget", 2d, true);
        clock.Advance(.09);
        Assert.IsTrue(Value(interpolation) > 1 && Value(interpolation) < 2);
        clock.Advance(.09);
        Assert.AreEqual(2d, Value(interpolation));
        Call(interpolation, "SetTarget", 3d, false);
        Assert.AreEqual(3d, Value(interpolation));
        interpolation = Create(clock, "BoundedProgressInterpolation");
        Call(interpolation, "SetTarget", 5d, true);
        for (int step = 1; step <= 6; step++)
        {
            clock.Advance(.05);
            Call(interpolation, "SetTarget", 5d + step * .0001, true);
        }
        Assert.IsTrue(Value(interpolation) >= 5, "Small estimates cannot postpone an authoritative stage catch-up deadline.");
    }
    [TestMethod]
    public void OpaqueProgressIsLabelledBoundedAndResetsPerStage()
    {
        var clock = new ManualClock();
        object estimator = Create(clock);
        object owner = new();
        Update(estimator, owner, 1, "first", null);
        clock.Advance(30);
        Assert.IsTrue(Value(estimator) > 0);
        Assert.IsTrue(Estimated(estimator));
        clock.Advance(100000);
        Assert.IsTrue(Value(estimator) <= .95);
        Update(estimator, owner, 2, "second", null);
        Assert.AreEqual(0d, Value(estimator));
    }

    [TestMethod]
    public void MeasurementsNeverBecomeElapsedTimeOrSyntheticHighWater()
    {
        var clock = new ManualClock();
        object estimator = Create(clock);
        object owner = new();
        Update(estimator, owner, 1, "step", null);
        clock.Advance(300);
        Assert.IsTrue(Value(estimator) > .2);
        Update(estimator, owner, 2, "step", .2);
        Assert.AreEqual(.2, Value(estimator));
        Assert.IsFalse(Estimated(estimator));
        clock.Advance(300);
        Assert.AreEqual(.2, Value(estimator));
        Update(estimator, owner, 3, "step", 1);
        Assert.AreEqual(1d, Value(estimator), "A measured phase may finish without claiming overall success.");
    }

    [TestMethod]
    public void FreezeStaleRevisionNewRunAndReducedMotionAreDeterministic()
    {
        var clock = new ManualClock();
        object estimator = Create(clock);
        object owner = new();
        Update(estimator, owner, 2, "step", .4);
        Update(estimator, owner, 1, "step", .8);
        Assert.AreEqual(.4, Value(estimator));
        Call(estimator, "Freeze");
        clock.Advance(300);
        Assert.AreEqual(.4, Value(estimator));
        Update(estimator, new object(), 1, "step", null, false, false);
        clock.Advance(300);
        Assert.AreEqual(0d, Value(estimator));
        Update(estimator, owner, 3, "step", null, true);
        Assert.AreEqual(1d, Value(estimator));
        Assert.IsFalse(Estimated(estimator));
        Call(estimator, "Reset");
        Assert.AreEqual(0d, Value(estimator));
    }

    [TestMethod]
    public void MotionToggleHoldsCurrentEstimateAndWallClockChangesCannotMoveIt()
    {
        var clock = new ManualClock();
        object estimator = Create(clock);
        object owner = new();
        Update(estimator, owner, 1, "step", null);
        clock.Advance(30);
        double before = Value(estimator);
        Update(estimator, owner, 2, "step", null, false, false);
        Assert.AreEqual(before, Value(estimator));
        clock.Advance(300);
        Assert.AreEqual(before, Value(estimator));
        Update(estimator, owner, 3, "step", null);
        Assert.AreEqual(before, Value(estimator));
        clock.MoveWallClock(-10000);
        Assert.AreEqual(before, Value(estimator));
        clock.Advance(10);
        Assert.IsTrue(Value(estimator) > before);
    }

    private static object Create(TimeProvider clock, string name = "BoundedProgressEstimator")
    {
        Type? type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(
            "GraniteEdgeAI.Presentation.Progress." + name)).FirstOrDefault(t => t is not null);
        Assert.IsNotNull(type, "The shared bounded estimator is missing.");
        return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { clock }, null)!;
    }

    private static object? Call(object instance, string method, params object?[] args) => instance.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static void Update(object estimator, object owner, long revision, object stage, double? fraction,
        bool complete = false, bool motion = true) => Call(estimator, "Update", owner, revision, stage, fraction, complete, motion);
    private static double Value(object estimator) => (double)Call(estimator, "GetFraction")!;
    private static bool Estimated(object estimator) => (bool)Call(estimator, "IsEstimated")!;

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        private long timestamp;
        public override DateTimeOffset GetUtcNow() => now;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        internal void Advance(double seconds) { now += TimeSpan.FromSeconds(seconds); timestamp += TimeSpan.FromSeconds(seconds).Ticks; }
        internal void MoveWallClock(double seconds) => now += TimeSpan.FromSeconds(seconds);
    }
}
