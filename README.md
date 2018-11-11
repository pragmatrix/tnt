# TNT - The .NET Translation Tool

A command line tool for managing translations based on strings extracted from .NET assemblies. Supports XLIFF roundtrips and machine translations.

Born out of my rejection of .NET resources files and their annoying identifiers pulluting all the code. 

Then, over a 140 commits later, `tnt` seems to get somehat useful. All the interesting decisions that had to be made, the experience to program in F#, and how good machine translations can be today, were worth every commit and the many hours put in.

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

`tnt` extracts tagged strings from .NET assemblies. For C#, each string that needs to be translated must appended with an extension method invocation `.t()` that does two things. First, it tags the string that comes before it, and second, `t()` translates the string if there is a suitable translation file available (I'll come to that later). 

To make the `.t()` function available to your projects, add the [TNT.T][TNT.T] NuGet package to your project and add a `using TNT;` on top of the files you wish to tag strings with. Lucky owners of Resharper may just type in `.t()` after a string and add the `using TNT;` by pressing ALT+Enter.

Before the extraction, you need to add at least one target language to the project. Add one, say "Spanisch" with `tnt add -l Spanish`. 

> You can either use language names or language tags, if you are not sure what languages are supported, `tnt` can show you a list of all supported .NET language tags and language names with `tnt show languages`. 

Now enter `tnt extract`. This command extracts the strings and creates the language files in `.tnt/translation-[tag].json`. `tnt` will show what's being done and will print a status for each translation file that.

> The status output consists of the language's tag, its counters, its language name, and the filename of the translation file.

> Of particular interest are the counters, these count the states the individual strings are in. If you extracted, say 5 strings and haven't translated them yet, you'll see here a `[5n]`. Later counters for additional states will appear. If you are interested now, skip to [TranslationStates](#TranslationStates) for more information.

### Translating Strings

`tnt` itself does not support interactive translations [yet](https://github.com/pragmatrix/tnt/issues/46). Of course you can edit the translation files, but `tnt` can help you in two other ways:

First. `tnt` supports Google machine translations, which I do recommend as a starting point for every new translation. For the machine translation I tried, the results needed minimal changes and Google's API positioned .NET placeholders like `{0}` as expected. I don't know if your experience will be the same, but with `tnt translate` you can try your luck with Google Cloud Translation API.

> To use Google Cloud Translation API, follow the steps 1. to 3. in [Quickstart](https://cloud.google.com/translate/docs/quickstart), and then invoke `tnt translate`, which will machine translate all your new strings.

Second, it supports the traditional translation roundrip that is comprised of exporting the translation files to the [XLIFF][XLIFF] format, using an XLIFF tool to edit them, and importing the changes back. With `tnt export`,  XLIFF files can be generated and sent to translators, who can then use their favorite tool (like the [Multilingual App Toolkit](https://developer.microsoft.com/en-us/windows/develop/multilingual-app-toolkit, for example)) to translate these strings and send them back. `tnt import` will then take care of the reintegration into the matching translation files.

> Although `tnt` tries hard to do its best, `tnt import` is one of the commands unexpected things may happen. So be sure to put the `.tnt/` under revision control.


### Deployment & Translation Loading

`tnt` automatically creates and maintains a second directory named `.tnt-content/` where it puts the translation files in that the final application uses.

> This directory *does not need* to be under revision control, because it can be regenerated from the translation files with `tnt sync`. Basically, these files are *distilled* translation files for each language optimized for your application to pick them up.
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

#### `tnt add`

Adds a new assembly to the list of sources, or a new language to the list of translations.

`-a`, `--assembly` Add an assembly to the list of sources. The argument provided _must_ be a relative path to the assembly.

`-l`, `--language` Adds a new target translation language.

#### `tnt remove`

Removes an assembly from the list of sources.

`-a`, `--assembly` Removes an assembly. The argument provided may be the assemblies' name or a sub-path of the relative path of the assembly. As long the assembly can be identified unambiguously, it's going to be removed from the list of sources. Use `tnt status -v` to list all the sources that are currently registered.

> Intentionally, `tnt` has no option for removing language files and requires you to delete the file with `rm` or your revision control's file delete method. To update the `.tnt-content/` directory use `tnt sync` after that.

#### `tnt extract`

Extracts the strings from all sources. If a string is already listed in the translation file, nothing changes, if it's not a string with the state `new` will be added. 

All the strings that were previously listed in the translation files but were missing from the sources, will be get the state `unused`. 

> If strings reappear in the sources, for example by changing them back or readding a `.t()` to them, they change from the state `unused` to `needs-review`. 
>
> To get rid of all `unused` strings, for example after all translations are finalized, use `tnt gc`. Also not that the translation files in `.tnt-content/` do not contain strings that are marked `unused`.

#### `tnt gc`

Deletes all the strings that are in the state `unused`. 

#### `tnt status`

Shows the status of all translations. See also [TranslationsStates](#TranslationStates).

`-v`, `--verbose` in addition to the translations, shows the formatted contents of the `sources.json` file.

#### `tnt export`

#### `tnt import`

#### `tnt translate`

#### `tnt sync`

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

Shows the strings and their contexts that are in the state `needs-review` and contain analysis warnings.

The details `new`, `unused`, `shared`, and `warnings` can be restricted to show information about specific translations only. Use `-l` or `--language` to restrict their scope. If no language is specified, all languages are considered.

#### `tnt help`

#### `tnt version`

### TranslationStates

A translation state define the state of a single string's translation. In the translation files, the state's are used in their long form, when listed as a counter, they are abbrevated with a single character.

- `new`, `n`  
  A string yet untranslated.
- `needs-review`, `n`  
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


