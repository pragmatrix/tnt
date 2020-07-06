paket=.paket/paket

.PHONY: setup
setup:
	${paket} install

version=${shell grep -Po '<Version>\K[0-9.]+' tnt/tnt.fsproj}

.PHONY: version
version:
	echo ${version}

push=paket push --api-key ${NUGETAPIKEY} 

.PHONY: install
install: pack update-tnt-local
	
.PHONY: publish-and-update
publish-and-update: publish update-tnt-wait

.PHONY: publish
publish: pack
	${push} tmp/tnt-cli/*.nupkg
	${push} tmp/TNT.T/*.nupkg
	${push} tmp/TNT.T.FSharp/*.nupkg

.PHONY: pack
pack:
	mkdir -p tmp
	mkdir -p tmp/tnt-cli
	mkdir -p tmp/TNT.T
	mkdir -p tmp/TNT.T.FSharp
	rm -f tmp/tnt-cli/*.nupkg
	rm -f tmp/TNT.T/*.nupkg
	rm -f tmp/TNT.T.FSharp/*.nupkg
	dotnet clean -c Release
	dotnet restore 
	cd tnt && rm -rf obj bin && dotnet pack -c Release -o ../tmp/tnt-cli
	cd TNT.T && rm -rf obj bin && dotnet pack -c Release -o ../tmp/TNT.T
	cd TNT.T.FSharp && rm -rf obj bin && dotnet pack -c Release -o ../tmp/TNT.T.FSharp

.PHONY: install-tnt
install-tnt:
	dotnet tool install -g tnt-cli 

.PHONY: update-tnt-local
update-tnt-local: pack
	dotnet tool update -g --configfile local-nuget.config tnt-cli

.PHONY: update-tnt
update-tnt:
	dotnet nuget locals http-cache --clear
	dotnet tool update -g tnt-cli

.PHONY: update-tnt-wait
update-tnt-wait:
	until make update-tnt | grep -m 1 "version '${version}'"; do (echo "waiting for version ${version} to appear..."; sleep 1); done

