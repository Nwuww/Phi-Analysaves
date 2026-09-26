# Analysaves
<!-- README-I18N:START -->
**English** | [汉语](./README/README.zh.md)
<!-- README-I18N:END -->
Analyze formatted save files using CLI

- `/save/` contains a save file used for testing (only ⑨)
- `/songlist/` contains a sample songlist
> The above files are not included in the release

Command list:
```
set save-path [PATH]    Set save path (when no argument, interactively select from the save/ directory)
set songlist [PATH]     Set song list path (when no argument, interactively select from the songlist/ directory)
set out-path <PATH>     Set export path (default export/out.txt)
set depth <int>|max     Set search depth (default max=all saves)
set feat <double...>    Set feature value list (0–100), e.g.: set feat 98 99 99.5
set avgsmfn|coeffsmfn [none|tanh|bisigmoid|pseudo-huber]
                        Set the smoothing function for fitting weights
clear                   Clear all cache files
reset                   Reset all settings and clear cache

status											                    Output the number of loaded saves
ana song -id <id> <EZ|HD|IN|AT> [-nosort]			  Enter song analysis mode by ID and difficulty; use -nosort to specify no sorting in the cache
ana song -name <keyword> <EZ|HD|IN|AT> [-nosort]	Enter song analysis mode by song name and difficulty
ana diff <constant> [-nosort]							          Enter chart constant analysis mode (supports ranges such as 17-17.3)

In analysis mode:
- avg						      Output the average
- med						      Output the median
- above <double>			Get the proportion above a certain value
- below <double>			Get the proportion below a certain value
- debug fitting			  Calculate the fitted chart constant
- exit				        Exit analysis mode
> Note: In analysis mode, specify -exp to export the output to export/

  help                    Show this help
  #exit                   Exit the system
```