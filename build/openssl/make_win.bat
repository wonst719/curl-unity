set BUILD_DIR=%CD%\build\win
set PREBUILT_DIR=%CD%\prebuilt\win
set OPENSSL_ROOT=openssl-1.1.1b

mkdir %BUILD_DIR%

pushd %OPENSSL_ROOT%
perl Configure VC-WIN64A --release --prefix=%PREBUILT_DIR% no-asm no-shared no-unit-test
set CL=/MP
nmake install_dev
popd