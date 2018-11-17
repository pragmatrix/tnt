# TNT - The .NET Translation Tool

A command line tool for managing translations based on strings extracted from .NET assemblies. Supports XLIFF roundtrips and machine translations.

Born out of my rejection of .NET resources files and their annoying identifiers polluting all the code.

Then, a number of commits later, `tnt` seems to get somehat useful. All the interesting decisions that had to be made, the experience to program in F#, and how good machine translations can be today, were worth every commit and the many hours put in.

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

`tnt` likes to work in a project's directory, preferable the directory of your application's primary project. Change into this directory an initialize `tnt` with `tnt init`. This creates the subdirectory `.tnt/` and the file `.tnt/sources.json`. The `.tnt/` directory contains all the _important_ files `tnt` is managing for you, specifically the list of sources and the translation files. 

> The `.tnt/` directory should be checked in into your repository. `tnt` may sometimes act somewhat unforgiving and does not offer an undo option, so this is not a recommendation. If you want to use `tnt`, check in the `.tnt/` directory and all its contents.

> `tnt init` creates a default source language `en-US`, the language your original strings are in. If your original string are in a different language, you can change it anytime with `tnt init -l [your language tag or name]`.

### Sources

A source is something `tnt` retrieves original strings from. Currently, `tnt` supports only .NET assemblies as sources.

All sources are listed in the `.tnt/sources.json` file and can be added by invoking `tnt add -a [relative path to the assembly]` from within your project's directory. For example `tnt add -a bin/Release/netcoreapp2.1/MyAwesomeApp.exe` would add an assembly to the list of sources.

> `tnt` does not read or modify any of your project or other files, it only accesses the sources, you specify.

### Language & Assembly Extraction

`tnt` extracts marked strings from .NET assemblies. For C#, each string that needs to be translated must appended with an extension method invocation `.t()` that does two things. First, it marks the string that comes before it, and second, `t()` translates the string if there is a suitable translation file available.

> For more extraction options take a look at [Methods of Extraction](#methods-of-extraction).

To make the `.t()` function available to your projects, add the [TNT.T][TNT.T] NuGet package to your project and add a `using TNT;` on top of the files you wish to mark translatable strings with. Lucky owners of Resharper may just type in `.t()` after a string and add the `using TNT;` by pressing ALT+Enter. 

Before extracting, you need to add at least one target language to the project. Add one, say "Spanisch" with `tnt add -l Spanish`. 

> You can use either language names or language tags. If you are not sure what languages are supported, use `tnt show languages` to show a list of all supported .NET language tags and language names.

Now enter `tnt extract`. This command extracts the strings and creates the language files in `.tnt/translation-[tag].json`. `tnt` will show what's being done and will output a status for each translation file.

> The status consists of the language's tag, its counters, its language name, and the filename of the translation file.

> Of particular interest are the counters, these count the states the individual strings are in. If you extracted, say 5 strings and haven't translated them yet, you'll see here a `[5n]`. Later, counters for additional states will appear. If you are interested, [TranslationStates](#TranslationStates) explains all the states and counters.

### Translating Strings

`tnt` itself does not support interactive translations [yet](https://github.com/pragmatrix/tnt/issues/46). Of course you can edit the translation files, but `tnt` can help you in other ways:

#### Machine Translations

First. `tnt` supports Google machine translations, which I do recommend as a starting point for every new translation. For the machine translations I tried, the results needed minimal changes and Google's API positioned .NET placeholders like `{0}` as expected. I don't know if the experience will be the same for your translations, but with `tnt translate` you can try your luck with Google Cloud Translation API.

> To use Google Cloud Translation API, follow the steps 1. to 3. in [Quickstart](https://cloud.google.com/translate/docs/quickstart), and then invoke `tnt translate` to translate all new strings.

#### XLIFF Roundtrips

Second, it supports the traditional translation roundrip that is comprised of exporting the translation files to the [XLIFF][XLIFF] format, using an XLIFF tool to edit them, and importing the changes back. With `tnt export`,  XLIFF files can be generated and sent to translators, who can then use their favorite tool (like the [Multilingual App Toolkit](https://developer.microsoft.com/en-us/windows/develop/multilingual-app-toolkit, for example)) to translate these strings and send them back. `tnt import` will then take care of the reintegration into the matching translation files.

> Although `tnt` tries hard to do its best, `tnt import` is one of the commands unexpected things may happen. So be sure to put the `.tnt/` directory under revision control.

#### Translation Verification

In addition to translation support, `tnt` supports a number of simple verifications it applies to the translations. `tnt` verifies

- if the same .NET placeholders (for example `{0}`) reappear in the translated text.
- if the number of lines match.
- if the indents match.

`tnt` verifies only the translations that are in the state `needs-review` (state `r` for short). If one of these verification failes, `tnt status` adds them to the warning (`w`) counter. To show them in detail, use `tnt show warnings`.


### Deployment & Translation Loading

`tnt` automatically creates and maintains a second directory named `.tnt-content/` where it puts the translation files in meant to be loaded by your application.

> These files *do not need* to be under revision control, because they can be regenerated from the translation files with `tnt sync`. Basically, they are *distilled* translation files for each language optimized for your application to pick them up.
>
> The format of these files might change in the future and so should not be relied on.

To make the application aware of these files, add them to the project and change its build action to `Content`.

> After adding the files to project, you can change the build action in the properties dialog or by changing the XML element of the files from `<Compile ...` to `<Content ...`

Now, when you start your application with another language configured, it should pick up the right translation file and translate the strings with the function `.t()`.

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

Languages can be selected either by specifying `--all` or by naming them as arguments. Language names and tags are accepted.

`--all` Select all existing languages to export. By default, no languages are exported.

`--to` Specify the directory to where the files should be exported. The default is the current directory.

`--for` Specify the tool that will used for editing the XLIFF files. Without `--for`, the XLIFF XML format is generated in a way that should be compatible with most XLIFF tools. `--for mat` will generate XLIFF that is compatible with the Multilingual App Toolkit.

> The [Multilingual App Toolkit needs](https://multilingualapptoolkit.uservoice.com/forums/231158-general/suggestions/10172562-fix-crash-when-opening-xliff-file-without-group) the `<trans-unit>` elements to be surrounded with a `<group>` element, otherwise it will fail opening the files. 
>
> `tnt` supports this as an option, because other tools may fail if they encounter `<group>` elements.

Examples:

`tnt export German` exports an XILFF file named after the current directory and the language to the current directory.

`tnt export en` exports all translations that begin with `en`.

`tnt export --all --for mat` exports all translations for the use with the Multilingual App Toolkit.

#### `tnt import`

Imports XLIFF files.

#### `tnt translate`

Translates new strings.

#### `tnt sync`

Regenerates the `.tnt-content/` directory and its files. Usually `tnt` automatically takes care of updating the final translation files, but in case of errors or if the `.tnt-content/` does not exist at all, it may be useful to be sure that the final translations match the translations in the `.tnt/` directory.

> If you decide not to check in the `.tnt-content/` directory, `tnt sync` should be part of your build process.

#### `tnt show`

Shows various infomations about the .NET supported languages or interesting details of the translations.

##### `tnt show languages`

Shows the currently supported languages of the .NET framework tnt runs in. The output lists all the support the language tags and the language names.

##### `tnt show new`

Shows the strings and their contexts that are not translated yet.

##### `tnt show unused`

Shows the strings and their contexts that are not used.

##### `tnt show shared`

Shows the strings and their contexts that were found at more than one context.

##### `tnt show warnings` 

Shows the strings and their contexts that are in the state `needs-review` and have one or more verification warnings.

The details `new` and `warnings` can be restricted to show information of specific translations only. Use `-l` or `--language` to filter their output. If no language is specified, all languages are considered.

> The output of `unused` and `shared` depends on the extracted strings only and is therefore independent of the languages specified.

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

#### File `.tnt/sources.json` 

#### File `.tnt/translation-[tag].json` 

### Directory `.tnt-content` 

#### File `.tnt-content/[tag].tnt` 

## License & Copyright

[MIT](LICENSE)

(c) 2018 Armin Sander


