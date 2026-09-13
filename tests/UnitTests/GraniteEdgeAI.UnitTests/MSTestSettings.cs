// run independent test methods in parallel as the test suite grows
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
