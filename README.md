# TNT - The .NET Translation Tool

A command line tool for managing translations based on strings extracted from .NET assemblies. Supports XLIFF roundtrips and machine translations.

## Why?

I did not like to use resource identifiers and then one thing lead to another.

## Installation & Update

To install `tnt`, download the [latest dotnet framework](https://www.microsoft.com/net/download) and enter:

```bash
dotnet tool install tnt-cli -g --add-source https://www.myget.org/F/pragmatrix/api/v2
```

After that `tnt` can be invoked from the command line. For example `tnt version` shows its current version. To update `tnt` enter:

```bash
dotnet tool update tnt-cli -g --add-source https://www.myget.org/F/pragmatrix/api/v3/index.json
```

## Concepts & Walkthrough

### Projects & Initialization & Subdirectories

`tnt` works in a project's directory, preferable the directory of your application's primary project. Change into this directory an initialize it with `tnt init`, which creates the subdirectory `.tnt/` and the file `.tnt/sources.json`. The `.tnt/` directory contains all the _important_ files `tnt` is managing for you, these are the list of sources and the translation files.

> The `.tnt/` directory is meant to be checked in into your repository. Note that `tnt` may sometimes act somewhat unforgiving and does not offer an undo option, so this is not a recommendation.

> `tnt init` sets the source language your original strings are in to `en-US` by default. If your original strings are in a different language, you can change that anytime with `tnt init -l [your language tag or name]`.

### Sources

A source is something `tnt` retrieves original strings from. Currently, `tnt` supports .NET assemblies as sources only.

All sources are listed in the `.tnt/sources.json` file and can be added by entering `tnt add -a [relative path to the assembly]` from within your project's directory. For example `tnt add -a bin/Release/netcoreapp2.1/MyAwesomeApp.exe` would add an assembly to the list of sources.

> `tnt` does not read or modify any of your project files, it accesses only the sources you specify.

### Language & Assembly Extraction

`tnt` extracts marked strings from .NET assemblies. For C#, each string that needs to be translated must be appended with an extension method `.t()` that does two things. First, it marks the string that comes before it, and second, it translates the string if there is a suitable translation file available.

> For more extraction options take a look at [Methods of Extraction](#methods-of-extraction).

To use the `.t()` function in your projects, add the [TNT.T][TNT.T] NuGet package to your project and insert a `using TNT;` on top of the files you wish to mark strings with. Lucky owners of Resharper may just type `.t()` after a string and insert the `using TNT;` by pressing ALT+Enter. 

Before extracting, you need to add at least one target language to the project. Add one, say "Spanisch" with `tnt add -l Spanish`. 

> With `tnt add` and with several other commands, you can use either language names or language tags. If you are not sure what languages are supported, use `tnt show languages` to list all supported .NET language tags and language names.

Now build your project to be sure the sources are available and then enter `tnt extract`, which extracts the strings and creates language files in `.tnt/translation-[tag].json`. `tnt` will show what it does and prints a status for each translation file.

> The status consists of the translation's language tag, its counters, its language name, and the filename of the translation file.

> Of particular interest are the counters that count the states the individual strings are in. If you extracted, say 5 strings, and haven't translated them yet, you'll see a `[5n]`. Later, counters for additional states will appear. If you are interested now, the section [TranslationStates](#TranslationStates) explains them all.

### Translating Strings

`tnt` itself does not support interactive translations [yet](https://github.com/pragmatrix/tnt/issues/46). Of course you can edit the translation files, but `tnt` can help you in other ways:

#### Machine Translations

First. `tnt` supports Google machine translations, which I can recommend as a starting point for every new string. For the English to German machine translations I tried, the results needed minimal changes and Google's translation AI positioned .NET placeholders like `{0}` at the locations expected. I don't know if the experience will be the same for your translations, but with `tnt translate` you can try your luck with Google Cloud Translation API.

> To use Google Cloud Translation API, follow the steps 1. to 3. in [Quickstart](https://cloud.google.com/translate/docs/quickstart), and then invoke `tnt translate` to translate all new strings.

#### XLIFF Roundtrips

Second, it supports the traditional translation roundrip that is comprised of exporting the translation files to the [XLIFF][XLIFF] format, using an XLIFF tool to edit them, and importing the changes back. With `tnt export`,  XLIFF files can be generated and sent to translators, who can then use their favorite tool (like the [Multilingual App Toolkit](https://developer.microsoft.com/en-us/windows/develop/multilingual-app-toolkit, for example)) to translate these strings and send them back. `tnt import` will then take care of the reintegration into the matching translation files.

> Although `tnt` tries hard to do its best, `tnt import` is one of the commands unexpected things may happen. So be sure that the `.tnt/` directory is under revision control.

#### Translation Verification

In addition to translation support, `tnt` supports a number of simple verifications it applies to the translated strings. `tnt` verifies

- if the same .NET placeholders (for example `{0}`) reappear in the translated text.
- if the number of lines match.
- if the indents match.

`tnt` verifies the translations that are in the `needs-review` state only (state `r` for short). If one of these verification failes, `tnt status` adds them to the warning (`w`) counter. To show them in detail, use `tnt show warnings`.


### Deployment & Translation Loading

`tnt` maintains a second directory named `.tnt-content/` where it puts translation files meant to be loaded by your application.

> These files *do not need* to be under revision control, because they can be regenerated from the translation files with `tnt sync`. Basically, they are *distilled* translation files for each language optimized for your application to pick them up.
>
> The format of these files might change in the future and so should not be relied on.

To make the application aware of these files, add them to the project and change its build action to `Content`.

> After adding the files to project, you can change the build action in the properties dialog or by changing the XML element of the files from `<Compile ...` to `<Content ...`

Now, when you start your application with another language configured, it should pick up the right translation file and translate the strings marked with `.t()`.

## Reference

### Command Line, Parameters, and Examples

The `tnt` command line tool uses a verb in the first position to define a specific task it should execute. The command line options coming after are either with a single character prefixed with `-` or a word prefixed with `--`. Some commands take free arguments.

For a list of commands use `tnt help`, and for the options of a specific task, use `tnt [task] --help`. For example `tnt init --help` shows what options `tnt init` has to offer.

#### `tnt init`

Initializes the `.tnt/` directory in the current directory. This is always the first command that needs to be used to start using `tnt`.

`-l`, `--language` (default `en-US`) Sets the source langage the strings extracted are in.

> `tnt init -l [language]` can be used to change the source language later on.

Examples:

`tnt init -l en-US` initializes `.tnt/` directory and sets the source language to `en-US`.

#### `tnt add`

Adds a new assembly to the list of sources, or a new language to the list of translations.

`-a`, `--assembly` Add an assembly to the list of sources. The argument provided _must_ be a relative path to the assembly.

`-l`, `--language` Adds a new target translation language.

Examples:

`tnt add -a bin/Release/netcoreapp2.1/MyAwesomApp.dll` adds the assembly to the list of sources.

#### `tnt remove`

Removes an assembly from the list of sources.

`-a`, `--assembly` Removes an assembly. The argument provided may be the assemblie's name or a sub-path of the assembly. As long the assembly can be identified unambiguously, it's going to be removed from the list of sources. Use `tnt status -v` to list all the sources that are currently registered.

> Intentionally, `tnt` has no option for removing language files and requires you to delete the file with `rm` or your revision control's delete method. To update the `.tnt-content/` directory use `tnt sync` after that.

#### `tnt extract`

Extracts the strings from all sources. If a string is already listed in the translation file, nothing changes.

All the strings that were previously listed in the translation files but were from the sources a later time, will be set to the state `unused`. 

> If strings reappear in the sources, for example by changing them back or marking them with a `.t()` again, they will change from the state `unused` to `needs-review` indicating the need for further attention.
>
> To get rid of all `unused` strings, for example after all translations are finalized, use `tnt gc`. Also note that the translation files in `.tnt-content/` do not provide strings marked `unused` to the application.

#### `tnt gc`

Deletes all the strings that are in the state `unused`. 

#### `tnt status`

Shows the status of all translations. See also [TranslationsStates](#TranslationStates).

`-v`, `--verbose` in addition to the translations, shows the formatted contents of the `sources.json` file.

#### `tnt export`

Export translations for use with a language translation tool.

This command will export translations into one file per each language. The files are named after the project name and the language tag. Currently, the project name is defined as the current directory's name.

`tnt export` will never overwrite any files. If files are already existing at the designated location's, `tnt` will print a warning and exit.

Languages can be selected either by `-l`, or by passing them as arguments. To select all languages, use `--all`. Language names and tags are accepted.

`--all` Select all existing languages to export. By default, no languages are exported.

`--to` Specify the directory to where the files should be exported. The default is the current directory.

`--for` Specify the tool that will used for editing the XLIFF files. Without `--for`, the XLIFF XML format is generated in a way that should be compatible with most XLIFF tools. `--for mat` will generate XLIFF that is compatible with the Multilingual App Toolkit.

`-l`, `--language` Used to specify language tags or names that should be exported. Currently, this is also possible by passing them as arguments.

> The [Multilingual App Toolkit needs](https://multilingualapptoolkit.uservoice.com/forums/231158-general/suggestions/10172562-fix-crash-when-opening-xliff-file-without-group) the `<trans-unit>` elements to be surrounded with a `<group>` element, otherwise it will fail opening the files. 
>
> `tnt` supports this as an option, because other tools may fail if they encounter `<group>` elements.

Examples:

`tnt export German` exports an XILFF file named after the current directory and the language to the current directory.

`tnt export en` exports all translations that begin with `en`.

`tnt export --all --for mat` exports all translations for the use with the Multilingual App Toolkit.

#### `tnt import`

Imports XLIFF files. `tnt import` can import either specific files or languages, or files that can find in the import directory. 

To import one or more languages, use `tnt import [language-or-tag]`, to import a file, use `tnt import [filename]`. To import all files that look like they were previously exported with `tnt export`, use `tnt import --all`.

`--from` The directory to import the files from. The default is the current directory.

`--all` Import all files that can be found in the import directory.

> The importer tries to match the original strings in the import files with the ones in the language files and if a matching string is found, _will overwrite_ its translation with the one imported. So before using `tnt import`, make sure the contents of the `.tnt/` directory is commited to your revision control system.

#### `tnt translate`

Translates new strings.

#### `tnt sync`

Regenerates the `.tnt-content/` directory and its files. Usually `tnt` automatically takes care of updating the final translation files, but in case of errors or if the `.tnt-content/` does not exist at all, it may be useful to be sure that the final translations match the translations in the `.tnt/` directory.

> If you decide not to check in the `.tnt-content/` directory, `tnt sync` should be part of your build process.

#### `tnt show`

Shows various infomations about the .NET supported languages or interesting details of the translations.

##### `tnt show languages`

Lists the currently supported language names and tags of the .NET framework `tnt` runs in.

##### `tnt show new`

Shows the strings and their contexts that are not translated yet.

##### `tnt show unused`

Shows the strings and their contexts that are not used.

##### `tnt show shared`

Shows the strings and their contexts that were found at more than one context.

##### `tnt show warnings` 

Shows the strings and their contexts that are in the state `needs-review` and have one or more verification warnings.

The details `new` and `warnings` can be restricted to show information of specific translations only. Use `-l` or `--language` to filter their result. If no language is specified, all languages are considered.

> The results of `unused` and `shared` depend on the extracted strings only and are therefore independent of the languages specified.

#### `tnt help`

#### `tnt version`

### Methods of Extraction

`tnt` extracts strings that are marked with a function that also translates them. This is the `t()` function that is located in `TNT.T` static class. 

There are a number of different ways to mark strings for translation:

#### Simple Strings

Constant and literal strings can be marked appending `.t()` to them. When the `t()` function is called, the string gets translated and returned. To use `t()`, add `using TNT;` to the top of your C# source file.

Examples:

`"Hello World".t()`

`string.Format("Hello {0}".t(), userName)`

#### String Interpolation

C# 6 [introduced string interpolation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated) by prefixing a string with `$`. To mark an interpolated string as translatable, the `t()` function is used, but - for technical reason - not as an extension method. Bringing the static `TNT.T` class into scope mitigates that:

```csharp
// bring the static t() method into scope.
using static TNT.T;
... 
... t($"Hello {World}") ...
```

> Note that the extracted string will result into `Hello {0}` for the example above.

#### Specific Translations

If the target language should be decided ad-hoc by the application, the `t()` function can be invoked with an additional argument that specifies language tag. For example `"Hello".t("es")` will translate the string "Hello" to spanish if the translation is available.

> Any extraction is done by scanning the IL code that is generated. If a `.t()` function is seen in the IL code an the extraction attempt fails, `tnt extract` will warn about that.

### TranslationStates

A translation state define the state of a single string's translation. In the translation files, the state's are used in their long form, when listed as a counter, they are abbrevated with a single character.

- `new`, `n`  
  A string yet untranslated.
- `needs-review`, `r`  
  Either machine translated, or imported from XLIFF indicating that a string is not final.
- `final`, `f`  
  Strings imported from XLIFF that were marked "translated" or "final".
- `unused`, `u`  
  A previously translated string that is missing from the list of sources after a recent `tnt extract`.

In addition to the state, the `w` counter shows the number of analysis warnings. To list the strings that contain warnings, use `tnt show warnings`.

### Directory `.tnt` 

This is the directory where `tnt` manages the translation sources and the translation files. The directory can be created with `tnt init`.

#### File `.tnt/sources.json` 

This file configures the language the original strings are in and the sources from where they are extracted.

Use `tnt init -l` to change the language, `tnt add` to add sources, and `tnt remove` to remove them.

#### File `.tnt/translation-[tag].json` 

The translation files. They contain the original strings with their translations and a state, a list of contexts, and notes for each translated string.

### Directory `.tnt-content` 

These contain translation files optimized for the application to load. 

#### File `.tnt-content/[tag].tnt` 

The language specific translation files. Currently, only the original and the translated strings.

`tnt` tries to keep these files up to date.

`tnt sync` may be used to regenerate them in case they are missing, an error happened, or language files in the `.tnt` directory were changed.

## License & Contribution & Copyright

[MIT](LICENSE) 

Contributions are welcome, please comply to the `.editorconfig` file.

(c) 2018 Armin Sander

