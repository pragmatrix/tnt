paket=.paket/paket

.PHONY: setup
setup:
	${paket} install

push=.paket/paket push --url https://www.myget.org/F/pragmatrix/api/v2/package --api-key ${MYGETAPIKEY} 

.PHONY: publish
publish:
	mkdir -p tmp
	rm -f tmp/*.nupkg
	dotnet clean -c Release
	cd tnt        && rm -rf obj bin && dotnet pack -c Release -o ../tmp
	cd TNT.FSharp && rm -rf obj bin && dotnet pack -c Release -o ../tmp
	cd TNT.CSharp && rm -rf obj bin && dotnet pack -c Release -o ../tmp
	${push} tmp/tnt-cli.*.nupkg
	${push} tmp/TNT.FSharp.*.nupkg
	${push} tmp/TNT.CSharp.*.nupkg
