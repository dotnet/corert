FROM corert/wasm AS corertwasmbuild

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS sdk

COPY --from=corertwasmbuild /d/corert/bin/WebAssembly.wasm.Debug /ILC

WORKDIR /d
COPY . .

RUN export IlcPath=/ILC && \
    dotnet publish -r wasm
