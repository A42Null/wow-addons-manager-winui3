#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成 installer/README.rtf（GBK 编码 RTF，供 Advanced Installer 打包为“显示 README”）。

用法： python installer/make_readme_rtf.py
输出： installer/README.rtf
"""
import os
import re

VERSION = "0.4.1"
PRODUCT = "WoW WinUI3 Addons Manager"

TEXT = f"""WoW WinUI3 Addons Manager（魔兽世界插件管理器）
版本：{VERSION}

安装位置
  默认安装到 C:\\Program Files\\WoWAddonManager，可在安装向导中修改。

首次使用
  1. 启动“魔兽世界插件管理器”。
  2. 打开“设置”页，点“自动检测”定位魔兽世界目录，并选择客户端版本后保存。
  3. 到“浏览插件”页选择插件源（CurseForge / 新手盒子 / 大脚插件站 / 大脚整合包），
     用版本筛选挑出匹配当前客户端的插件，点“安装”即可。

注意
  * 本程序为自包含发布，无需另外安装 .NET 或 Windows App SDK 运行时。
  * 安装/更新插件时，若已存在同名插件，旧版本会自动改名为 *.bak-时间戳 保留，不会被删除。
  * 卸载请使用“设置 → 应用 → 卸载程序”中的卸载项，配置文件会保留在用户目录。

卸载
  控制面板 → 程序和功能 → WoW WinUI3 Addons Manager → 卸载。
"""


def rtf_escape(text: str) -> str:
    out = []
    for ch in text:
        if ch in "\\{}":
            out.append("\\" + ch)
        elif ch == "\n":
            out.append("\\par\n")
        elif ord(ch) < 128:
            out.append(ch)
        else:
            # GBK 可编码 → 直接写入；否则用 \uN? 转义
            try:
                ch.encode("gbk")
                out.append(ch)
            except UnicodeEncodeError:
                code = ord(ch)
                out.append("\\u%d?" % (code if code < 32768 else code - 65536))
    return "".join(out)


def main() -> None:
    here = os.path.dirname(os.path.abspath(__file__))
    body = rtf_escape(TEXT)
    rtf = (
        "{\\rtf1\\ansi\\ansicpg936\\deff0\\deflang2052\n"
        "{\\fonttbl{\\f0\\fnil\\fcharset134 Microsoft YaHei;}}\n"
        "\\viewkind4\\uc1\\pard\\sa120\\sl252\\slmult1\\f0\\fs20\n"
        + body
        + "}\n"
    )
    out = os.path.join(here, "README.rtf")
    with open(out, "wb") as f:
        f.write(rtf.encode("gbk", errors="replace"))
    print("written:", out, len(rtf), "chars")


if __name__ == "__main__":
    main()
