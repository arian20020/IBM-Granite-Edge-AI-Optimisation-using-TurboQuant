param(
    [Parameter(Mandatory = $true)][string]$Checkout,
    [Parameter(Mandatory = $true)][ValidateSet('cpu', 'sycl', 'vulkan')][string]$Backend,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [int]$Parallel = 4,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$tools = Join-Path $env:APPDATA 'Python\Python311\Scripts'
$vsDevCmd = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$oneApi = 'C:\Program Files (x86)\Intel\oneAPI\setvars.bat'
$vulkanSdk = 'C:\VulkanSDK\1.4.350.0'
$build = Join-Path $Checkout ("build-{0}-controlled" -f $Backend)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

$flags = @('-DGGML_NATIVE=OFF', '-DGGML_AVX2=ON', '-DLLAMA_BUILD_TESTS=ON', '-DLLAMA_BUILD_EXAMPLES=ON', '-DLLAMA_BUILD_TOOLS=ON', '-DLLAMA_BUILD_SERVER=ON')
$prefix = "set PATH=$tools;%PATH%&& call `"`"$vsDevCmd`"`" -arch=x64 -host_arch=x64 >nul"
if ($Backend -eq 'sycl') {
    $prefix += " && call `"`"$oneApi`"`" >nul"
    $flags += @('-DGGML_SYCL=ON', '-DGGML_SYCL_TARGET=INTEL')
} elseif ($Backend -eq 'vulkan') {
    $prefix += " && set VULKAN_SDK=$vulkanSdk&& set PATH=$vulkanSdk\Bin;%PATH%"
    $flags += '-DGGML_VULKAN=ON'
}

$quotedFlags = $flags -join ' '
$configure = "$prefix && cmake -S `"$Checkout`" -B `"$build`" -G Ninja -DCMAKE_BUILD_TYPE=Release $quotedFlags"
$buildCommand = "$prefix && cmake --build `"$build`" --parallel $Parallel"
@{ backend = $Backend; checkout = $Checkout; build = $build; flags = $flags } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'command.json') -Encoding utf8
cmd.exe /d /s /c $configure 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'configure.log')
if ($LASTEXITCODE -ne 0) { throw "configure failed: $LASTEXITCODE" }
cmd.exe /d /s /c $buildCommand 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'build.log')
if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" }
if ($Test) {
    $testPrefix = $prefix
    if ($Backend -eq 'sycl') {
        $testPrefix += ' && set ONEAPI_DEVICE_SELECTOR=opencl:gpu&& set GGML_SYCL_ENABLE_FLASH_ATTN=0'
    }
    cmd.exe /d /s /c "$testPrefix && ctest --test-dir `"$build`" --output-on-failure" 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'ctest.log')
    if ($LASTEXITCODE -ne 0) { throw "tests failed: $LASTEXITCODE" }
}
