paket=.paket/paket

.PHONY: setup
setup:
	${paket} install

.PHONY: publish
publish:
	mkdir -p tmp
	rm -f tmp/*.nupkg
	dotnet clean -c Release
	cd tnt && dotnet pack -c Release -o ../tmp
	.paket/paket push --url https://www.myget.org/F/pragmatrix/api/v2/package --api-key ${MYGETAPIKEY} tmp/*.nupkg


