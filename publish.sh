#!/bin/bash
set -e
MSBuild.exe -nologo -verbosity:quiet TNT.sln -t:Clean -p:Configuration=Release
MSBuild.exe -nologo -verbosity:quiet TNT.sln -t:Restore -p:Configuration=Release
MSBuild.exe -nologo -verbosity:quiet TNT.sln -t:Build -p:Configuration=Release
mkdir -p tmp
rm -f tmp/*.nupkg
(cd tnt && dotnet pack --no-build -c Release -o ../tmp)
.paket/paket push --url https://www.myget.org/F/pragmatrix/api/v2/package --api-key $MYGETAPIKEY tmp/*.nupkg

