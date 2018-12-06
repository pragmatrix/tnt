# TNT - The .NET Translation Tool

A command line tool for managing translations based on strings extracted from .NET assemblies. Supports translation roundtrips via Excel or XLIFF and Google machine translations.

`tnt` lets you mark literal strings in source code, extracts them from the compiled IL code, and manages all further translation processes. At runtime, the NuGet [TNT.T][TNT.T] translates the marked strings.

In that regard, `tnt` is very similar to translation solutions like [gettext][gettext].

I've created `tnt` to provide an alternative for resource files.

[gettext]: https://en.wikipedia.org/wiki/Gettext

## Installation & Update

To install `tnt`, download the [latest dotnet framework](https://www.microsoft.com/net/download) and enter:

```bash
dotnet tool install tnt-cli -g --add-source https://www.myget.org/F/pragmatrix/api/v2
```

After that, `tnt` can be invoked from the command line. For example, `tnt version` shows its current version. To update `tnt` enter:

```bash
dotnet tool update tnt-cli -g --add-source https://www.myget.org/F/pragmatrix/api/v3/index.json
```

## Concepts & Walkthrough

### Projects, Initialization, and Subdirectories

`tnt` works in a project's directory, preferable the directory of your application's primary project. Change into this directory an initialize it with `tnt init`. This creates the subdirectory `.tnt/` and the file `.tnt/sources.json`. The `.tnt/` directory contains all the _important_ files that are managed for you: these are the list of sources and the translation files.

> Note that some `tnt` commands act somewhat unforgiving and do not offer an undo option, so I recommend to put the `.tnt/` directory under revision control.

> `tnt init` sets the source language of your project's strings to `en-US` by default. If your original strings are in a different language, you can change that anytime with `tnt init -l [your language tag or name]`.

### Sources

A source is something `tnt` retrieves original strings from. Currently, `tnt` supports .NET assemblies only.

All sources are listed in the `.tnt/sources.json` file and can be added by entering `tnt add -a [relative path to the assembly]` from within your project's directory. For example `tnt add -a bin/Debug/netcoreapp2.1/MyAwesomeApp.exe` would add an assembly to the list of sources.

> `tnt` does not read or modify any of your other project files, it accesses the sources only.

### Language & Assembly Extraction

`tnt` extracts marked strings from .NET assemblies. For C#, each string that needs to be translated must be marked with an extension method `.t()` that does two things: First, it marks the literal string that comes before it, and second, it translates the string. For example: `"Hello World".t()`.

> For more extraction options take a look at [Methods of Extraction](#methods-of-extraction).

To use the `.t()` function in your projects, add the [TNT.T][TNT.T] NuGet package to your project and insert a `using TNT;` to the top of the file you wish to mark strings with. Lucky owners of Resharper may just type `.t()` after a literal string and insert `using TNT;` by pressing ALT+Enter. 

Before extracting, you need to add at least one target language to the project. Add one, say "Spanisch" with `tnt add -l Spanish`. 

> `tnt add -l` and other commands accept language names or language tags. If you are not sure what languages are supported, use `tnt show languages` to list all .NET language tags and language names.

To be sure the sources are available, build your project and then enter `tnt extract` to extract the strings and to create the appropriate language files. 

While `tnt` extracts the strings, it shows what it does and prints a status for each language file generated. After the extraction, the language files are saved to `.tnt/translation-[tag].json`.

> The status consists of the translation's language tag, its counters, its language name, and the filename of the translation file.

> Of particular interest are the counters that count the states the individual strings are in. If you extracted, say 5 strings, and haven't translated them yet, you'll see a `[5n]`. Later, counters for additional states will appear. If you are interested now, [TranslationStates](#TranslationStates) explains them all.

### Translating Strings

`tnt` itself does not support the interactive translation of your strings, [yet](https://github.com/pragmatrix/tnt/issues/46). 

Of course, editing the translation files is possible, but there are other ways `tnt` can help you with:

#### Machine Translations

`tnt` supports Google machine translations, which should be a starting point for newly extracted strings. For the English to German machine translations I tried so far, the results were of good quality and Google's translation algorithm positioned .NET placeholders like `{0}` at the locations expected. I don't know if the resulting quality will be the same for your translations, but with `tnt translate` you can try your luck with the Google Cloud Translation API. For more information, skip to the section about [`tnt translate`](#`tnt translate`).

#### Excel Roundtrips

`tnt export` exports languages to a Excel file that can be modified by a translator and imported back with `tnt import`.

> Although `tnt` tries hard to do its best, `tnt import` is one of the commands unexpected things may happen. So please be sure that the `.tnt/` directory is under revision control.

#### XLIFF Roundtrips

Similar to the Excel roundtrips, `tnt` supports the traditional translation process that is comprised of exporting the translation files to the [XLIFF][XLIFF] format, using an XLIFF tool to edit them, and importing back the changes. With `tnt export`,  XLIFF files are generated and sent to translators, who can then use their favorite tool (like the [Multilingual App Toolkit](https://developer.microsoft.com/en-us/windows/develop/multilingual-app-toolkit, for example)) to translate these strings and send them back to the developer. After that, `tnt import` is used to update the strings in the translation files.

#### Translation Verification

`tnt` supports a number of simple verification rules it applies to the translated strings. `tnt` verifies

- if the same .NET placeholders (for example `{0}`) reappear in the translated text.
- if the number of lines match.
- if the indents match.

These rules are verified with each `tnt status` invocation for translations that are in the `needs-review` state only (`r` for short). If one of the rules fail to verify, `tnt status` increases the warning counter (abbrevated with `w`) and `tnt show warnings` may be used to show the messages in detail.


### Deployment & Translation Loading

`tnt` maintains a second directory named `.tnt-content/` where it puts translation files intended to be loaded by your application via the NuGet [TNT.T][TNT.T].

> These files *do not need* to be under revision control, because they can be regenerated with `tnt sync` any time. They contain *distilled* translations for each language optimized for your application to pick up.
>
> The format of these files might change in the future and should not be relied on.

To make the application aware of the translation files, add them to the project and change their build action to `Content`.

> After adding the files to the project, you can change the build action in the properties dialog of Visual Studio or by changing the XML element of the files from `<Compile ...` to `<Content ...`.

Now, when you start your application with another default user interface language configured, [TNT.T][TNT.T] loads the matching translation file and translates the strings marked with `.t()`.

## Reference

### Command Line, Parameters, and Examples

The `tnt` command line tool uses the first argument to decide what task it executes. Options are specified either with single character prefixed with `-` or a word prefixed with `--`. Some tasks take additional arguments.

For a list of available tasks use `tnt help`, and for options of a specific task, use `tnt [task] --help`. For example, `tnt init --help` shows what `tnt init` has to offer.

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

Export translations for use with a language translation tool or Excel.

This command will export translations to one file per each language. The files are named after the name of the project and the language tag. By default, the project name is the current directory's name.

`tnt export` will never overwrite any files. If files exist at the designated locations, `tnt` will output a warning and exit.

Languages can be selected either by `-l`, or by passing them as arguments. To select all languages, use `--all`. 

`--all` Select all existing languages to export. By default, no languages are exported.

`--to` Specify the directory where the files should be exported to. The default is the current directory.

`--for` Specify the tool that will used for editing the exported files. Supported formats are `excel`, `xliff`, and `xliff-mat` where `excel` is the default.

- `excel` Exports the translations into an Excel file that contains one workbook for each of the translation states. The workbooks contain the following columns:

  - The original strings.
  - An empty column that is meant to be filled with the translated string.
  - A column that contains the translated strings at the time of the export.
  - The state of the string.
  - Contexts that describe where the string appeared in the source.
  - Notes.

  Only the empty column and the state column are meant to be modified by the translator. 

  If there is already a translation available in the third column (for example a machine translated suggestion), the translator can copy it to the second and change it.

- `xliff` Exports the translations int the XLIFF 1.2 format that should be compatible with most XLIFF tools.

- `xliff-mat` exports the translations so that it's compatible with the Multilingual App Toolkit.

  > The [Multilingual App Toolkit needs](https://multilingualapptoolkit.uservoice.com/forums/231158-general/suggestions/10172562-fix-crash-when-opening-xliff-file-without-group) the `<trans-unit>` elements to be surrounded with a `<group>` element, otherwise it will fail to open the exported files. 
  >
  > `tnt` supports this as an option, because other tools may fail if they encounter `<group>` elements.

`-l`, `--language` Used to specify language tags or names that should be exported. Alternatively, the languages can be passed directly as arguments.

Examples:

`tnt export German` exports an XILFF file named after the current directory and the language to the current directory.

`tnt export en` exports all translations with a language tag `en` or  `en-*`.

`tnt export --all --for excel-mat` exports all translations for the use with the Multilingual App Toolkit.

#### `tnt import`

Imports Excel or XLIFF translation files. `tnt import` can import either specific files or languages, or files that can be found in the import directory. 

To import one or more languages, use `tnt import [language tag or name]`. To import a file, use `tnt import [filename]`. To import all files that look like they were previously exported with [`tnt export`](#`tnt export`), use `tnt import --all`.

`--from` The directory to import the files from. The default is the current directory.

`--all` Import all files that can be found in the import directory.

`-l`, `--language` Specify additional language tags or names to import. This option is an alternative to passing the languages as arguments.

> The importer matches the original strings in the files to import with the ones in the language files, and if a matching original string is found, _will overwrite_ the translation with the one imported. So before using `tnt import`, make sure the contents of the `.tnt/` directory is commited to your revision control system.

#### `tnt translate`

Machine-translates new strings. 

`--all` Translates all new string of all languages.

`-l`, `--language` Specifies the languages of which their new strings should be translated.

> Before `tnt` can be used to translate strings, it must be configured to use a machine translation service. For now, the Google Cloud Translation API is supported only. 
>
> To configure `tnt` to work with the Google Cloud Translation API, follow the steps 1. to 3. in [Quickstart](https://cloud.google.com/translate/docs/quickstart) and then use `tnt translate` to translate all new strings. 

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

Shows the strings and their contexts that are in the state [`needs-review`](#TranslationStates) and have one or more verification warnings.

The details `new` and `warnings` can be restricted to show information of specific translations only. Use `-l` or `--language` to filter their result. If no language is specified, all languages are considered.

> The results of `unused` and `shared` depend on the extracted strings only and are therefore independent of the languages specified.

#### `tnt help`

Shows useful hints about how to use the command line arguments. To show help for specific command verbs, use `tnt [command] --help`.

#### `tnt version`

Shows the current version of `tnt`.

### Methods of Extraction

`tnt` extracts strings that are marked with a function that also translates them. This is the `t()` function that is located in `TNT.T` static class. 

There are a number of different ways to mark strings for translation:

#### Simple Strings

Constant and literal strings can be marked appending `.t()` to them. When the `t()` function is called, the string gets translated and returned. To use `t()`, add `using TNT;` to the top of your C# source file.

Examples:

`"Hello World".t()`

`string.Format("Hello {0}".t(), userName)`

#### String Interpolation

C# 6 [introduced string interpolation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated) by prefixing a string with `$`. To mark an interpolated string as translatable, the `t()` function is used, but - for technical reason - not as an extension method. 

Bringing the static `TNT.T` class into scope mitigates that:

```csharp
// bring the static t() method into scope.
using static TNT.T;
... 
... t($"Hello {World}") ...
```

> Note that the extracted string will result into `Hello {0}` for the example above.

#### Specific Translations

If the target language should be decided ad-hoc by the application, the `t()` function can be invoked with an additional argument that specifies language tag. For example `"Hello".t("es")` will translate the string "Hello" to Spanish if the translation is available.

> Any extraction is done by searching through the generated IL code. If an invocation to the `.t()` function is found and the extraction attempt fails, [`tnt extract`](#`tnt extract`) will warn about that.

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

In addition to the state, the `w` counter shows the number of analysis warnings. To list the strings that contain warnings, use [`tnt show warnings`](#`tnt show warnings`).

### Directory `.tnt` 

This is the directory where `tnt` manages the translation sources and the translation files. The directory can be created with [`tnt init`](#`tnt init`).

#### File `.tnt/sources.json` 

This file configures the language the original strings are in and the sources from where they are extracted.

Use [`tnt init -l`](#`tnt init`) to change the language, [`tnt add`](#`tnt add`) to add sources, and [`tnt remove`](#`tnt remove`) to remove them.

#### File `.tnt/translation-[tag].json` 

The translation files. They contain the original strings with their translations and a state, a list of contexts, and notes for each translated string.

### Directory `.tnt-content` 

These contain translation files optimized for the application to load. 

#### File `.tnt-content/[tag].tnt` 

The language specific translation files. Currently, only the original and the translated strings.

`tnt` tries to keep these files up to date.

[`tnt sync`](#`tnt sync`) may be used to regenerate them in case they are missing, an error happened, or language files in the `.tnt` directory were changed.

## License & Contribution & Copyright

[MIT](LICENSE) 

Contributions are welcome, please comply to the `.editorconfig` file.

(c) 2018 Armin Sander



[TNT.T]: 