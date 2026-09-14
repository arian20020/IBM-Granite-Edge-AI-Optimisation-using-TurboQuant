// The packaged WinUI host owns one desktop UI thread. Run one test at a time
// so independent test windows cannot race during activation or shutdown.
[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.MethodLevel)]
