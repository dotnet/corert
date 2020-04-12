#Start from a slimmed down image of Debian 10 (Buster)
FROM debian:buster-slim AS LLVM9

#Install LLVM 9 so we can get libLLVM.so version 9 for Debian Buster
RUN apt-get update && apt-get -y install wget gnupg && \
    wget -O - https://apt.llvm.org/llvm-snapshot.gpg.key|apt-key add - && \
    printf "\ndeb http://apt.llvm.org/buster/ llvm-toolchain-buster-9 main\ndeb-src http://apt.llvm.org/buster/ llvm-toolchain-buster-9 main\n" >> /etc/apt/sources.list.d/llvm.list && \
    apt-get update && apt-get -y install -t llvm-toolchain-buster-9 clang-tools-9


#************************************************************************
#************************************************************************


#Start again from a slimmed down image of Debian 10 (Buster)
#This is the container we'll use to build CoreRT for wasm.
FROM debian:buster-slim

#Install prerequisites for ILC and Emscripten
RUN apt-get update && apt-get -y install cmake clang libicu-dev uuid-dev libcurl4-openssl-dev zlib1g-dev libkrb5-dev libtinfo5 git python2.7 wget

#Copy libLLVM.so version 9 from LLVM9 image that we made above
#This is needed by ILC Wasm - but since otherwise the build runs smoother with the clang 7
#which is the default version on Debian Buster, we don't want to overwrite the whole installation of llvm to v9.
COPY --from=LLVM9 /usr/lib/llvm-9/lib/libLLVM.so /usr/lib/libLLVM.so

#Use /d as a basis for our installation (development dir)
WORKDIR /d

#Install Emscripten version 1.39.12 into /d/emsdk
RUN git clone https://github.com/emscripten-core/emsdk.git --depth=1 --branch=1.39.12 && \
    cd emsdk && \
    ./emsdk install latest && \
    ./emsdk activate latest

#Copy corert files into /d/corert
WORKDIR /d/corert
COPY . .

#Use bash (not sh) for these scripts
SHELL ["/bin/bash", "-c"]

#Compile CoreRT/ILC for Wasm:
RUN cd /d/emsdk && . ./emsdk_env.sh && \
    export EMSCRIPTEN=/d/emsdk/upstream/emscripten && \
    export CppCompilerAndLinker=clang-7 && \
    export PATH=$PATH:/usr/lib/llvm-7/bin && \
    cd /d/corert && ./build.sh wasm

CMD ["/bin/bash"]
