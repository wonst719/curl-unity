OPEN_SSL_VERSION=openssl-1.1.1b

if [ ! -d $OPEN_SSL_VERSION ]; then    
    tar xzf ${OPEN_SSL_VERSION}.tar.gz
fi

do_make()
{
    PREBUILT_DIR=`pwd`/prebuilt/ios64
    export XCODE=`xcode-select --print-path`
    export CC='${XCODE}/Toolchains/XcodeDefault.xctoolchain/usr/bin/clang -fembed-bitcode'
    export CROSS_TOP=${XCODE}/Platforms/iPhoneOS.platform/Developer
    export CROSS_SDK=iPhoneOS.sdk

    (
        cd $OPEN_SSL_VERSION
        ./Configure ios64-cross --prefix=$PREBUILT_DIR no-shared no-dso no-hw no-engine
        make clean
        make install_dev -j8
    )
    
    mkdir -p $PREBUILT_DIR
}

do_make32()
{
    PREBUILT_DIR=`pwd`/prebuilt/ios32
    export XCODE=`xcode-select --print-path`
    export CC='${XCODE}/Toolchains/XcodeDefault.xctoolchain/usr/bin/clang -fembed-bitcode'
    export CROSS_TOP=${XCODE}/Platforms/iPhoneOS.platform/Developer
    export CROSS_SDK=iPhoneOS.sdk

    (
        cd $OPEN_SSL_VERSION
        ./Configure ios-cross --prefix=$PREBUILT_DIR no-shared no-dso no-hw no-engine
        make clean
        make install_dev -j8
    )
    
    mkdir -p $PREBUILT_DIR
}

do_make
do_make32

rm -r `pwd`/prebuilt/ios
mkdir `pwd`/prebuilt/ios

cp -r `pwd`/prebuilt/ios64/* `pwd`/prebuilt/ios
rm `pwd`/prebuilt/ios/lib/*.a

lipo -create `pwd`/prebuilt/ios64/lib/libssl.a `pwd`/prebuilt/ios32/lib/libssl.a -output `pwd`/prebuilt/ios/lib/libssl.a
lipo -create `pwd`/prebuilt/ios64/lib/libcrypto.a `pwd`/prebuilt/ios32/lib/libcrypto.a -output `pwd`/prebuilt/ios/lib/libcrypto.a
