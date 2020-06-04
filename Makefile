paket=.paket/paket

.PHONY: setup
setup:
	${paket} install

version=${shell grep -Po '<Version>\K[0-9.]+' tnt/tnt.fsproj}

.PHONY: version
version:
	echo ${version}

push=.paket/paket push --url https://www.myget.org/F/pragmatrix/api/v2/package --api-key ${MYGETAPIKEY} 

.PHONY: install
install: pack update-tnt-local
	
.PHONY: publish-and-update
publish-and-update: publish update-tnt-wait

.PHONY: publish
publish: pack
	${push} tmp/tnt-cli.*.nupkg
	${push} tmp/TNT.T.*.nupkg

.PHONY: pack
pack:
	mkdir -p tmp
	rm -f tmp/*.nupkg
	dotnet clean -c Release
	dotnet restore 
	cd tnt && rm -rf obj bin && dotnet pack -c Release -o ../tmp
	cd TNT.T && rm -rf obj bin && dotnet pack -c Release -o ../tmp

.PHONY: install-tnt
install-tnt:
	dotnet tool install -g --add-source https://www.myget.org/F/pragmatrix/api/v3/index.json tnt-cli 


.PHONY: update-tnt-local
update-tnt-local:
	dotnet tool update -g --configfile local-nuget.config tnt-cli

.PHONY: update-tnt
update-tnt:
	dotnet nuget locals http-cache --clear
	dotnet tool update -g --add-source https://www.myget.org/F/pragmatrix/api/v3/index.json tnt-cli

.PHONY: update-tnt-wait
update-tnt-wait:
	until make update-tnt | grep -m 1 "version '${version}'"; do (echo "waiting for version ${version} to appear..."; sleep 1); done

