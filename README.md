# Analysaves
使用CLI分析格式化后的存档文件

命令列表：
```
set save-path [PATH]    设置存档路径（无参数时从 save/ 目录交互选择）
set songlist [PATH]     设置曲目列表路径（无参数时从 songlist/ 目录交互选择）
set out-path <PATH>     设置导出路径（默认 export/out.txt）
set depth <int>|max     设置检索深度（默认 max=全部存档）
set feat <double...>    设置特征值列表（0~100），如: set feat 98 99 99.5
set avgsmfn|coeffsmfn [none|tanh|bisigmoid|pseudo-huber]
clear                   清除所有缓存文件
reset                   重置所有设置并清除缓存

status											                    输出读入的存档个数
ana song -id <id> <EZ|HD|IN|AT> [-nosort]			  通过 ID 和难度进入歌曲分析模式，使用-nosort指定在缓存中不排序
ana song -name <关键词> <EZ|HD|IN|AT> [-nosort]	通过歌名和难度进入歌曲分析模式
ana diff <定数> [-nosort]							          进入定数分析模式（支持区间如 17-17.3）

在分析模式下:
- avg						      输出平均值
- med						      输出中位数
- above <double>			获取超过某个值的占比
- below <double>			获取低于某个值的占比
- debug fitting			  计算拟合定数
- exit				        退出分析模式
> 注：在分析模式下指定-exp可导出输出内容到export/

  help                    显示本帮助
  #exit                   退出系统
```
