using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportOwnershipContractTests
{
    [TestMethod]
    public void ModelImportOwnsSelectionAndRaisesIntent_ButDoesNotOwnFrameNavigation()
    {
        Type pageType = typeof(ModelImportPage);

        Assert.IsNotNull(pageType.GetMethod(
            "TryRequestModelInspection",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.IsFalse(pageType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(member => member.Name.Contains("Frame", StringComparison.OrdinalIgnoreCase) ||
                           member.Name.StartsWith("Navigate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void FolderLocalPathIsAnActivePagePrivateCapability_NotAnEventOrConversionPayload()
    {
        MethodInfo localPathAccessor = typeof(ModelImportPage).GetMethod(
            "TryGetAcceptedFolderLocalPath",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        Assert.IsNotNull(localPathAccessor);
        Assert.AreEqual(typeof(bool), localPathAccessor.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(ModelSelectionOperationId), typeof(string).MakeByRefType() },
            localPathAccessor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

        Type[] handoffEventTypes =
        [
            typeof(OpenVinoInspectionRequestedEventArgs),
            typeof(SourceModelConversionRequestedEventArgs),
        ];
        foreach (Type type in handoffEventTypes)
        {
            Assert.IsFalse(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(property => property.PropertyType == typeof(string) &&
                                 property.Name.Contains("local", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [TestMethod]
    public void AuthorizedI0HandoffTypesRemainDistinctFromD1OwnedTypes()
    {
        Type[] d1Types =
        [
            typeof(ModelImportPage),
            typeof(ModelSelectionInput),
            typeof(ModelSelectionResult),
            typeof(OpenVinoInspectionRequestedEventArgs),
            typeof(SourceModelConversionRequestedEventArgs),
        ];

        foreach (Type type in d1Types)
        {
            StringAssert.Contains(type.Namespace!, ".Features.ModelImport");
        }

        // The shell relays the D1-owned immutable intent without creating a
        // second conversion payload or gaining a path capability.
        StringAssert.Contains(typeof(OnboardingShellPage).Namespace!, ".Features.Onboarding");
        StringAssert.Contains(typeof(SourceModelConversionRequestedEventArgs).Namespace!, ".Features.ModelImport");
        Assert.AreEqual(typeof(ModelImportPage).Assembly, typeof(OnboardingShellPage).Assembly,
            "The ownership contract is namespace and handoff based, not an assembly split.");
    }

    [TestMethod]
    public void ExistingGgufInspectionRequestIsTheExplicitI0PathBearingException()
    {
        PropertyInfo[] properties = typeof(ModelInspectionRequestedEventArgs)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.AreEqual(1, properties.Length);
        Assert.AreEqual("Request", properties[0].Name);
        Assert.AreEqual(typeof(ModelInspectionRequest), properties[0].PropertyType);
        Assert.IsTrue(typeof(ModelInspectionRequest).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(property => property.Name.Equals("ModelPath", StringComparison.Ordinal)));
    }
}
