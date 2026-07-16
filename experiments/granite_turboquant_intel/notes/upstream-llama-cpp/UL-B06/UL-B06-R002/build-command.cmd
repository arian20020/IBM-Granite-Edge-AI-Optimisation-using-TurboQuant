@call "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" intel64 --force
@"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build "C:\Users\Student\gtq-build\llama-b9870-sycl-release-001" --parallel 8
