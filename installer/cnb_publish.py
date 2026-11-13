#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""CNB Release API 响应解析助手（供 GitHub Actions 的 publish-cnb 步骤调用）。

用法：
    cat response.json | python3 installer/cnb_publish.py id <tag>          # 打印匹配的 release id
    cat response.json | python3 installer/cnb_publish.py upload_url        # 打印 upload_url
    cat response.json | python3 installer/cnb_publish.py json              # 原样打印（调试用）
"""
import json
import sys


def load(stream):
    raw = stream.read()
    try:
        return json.loads(raw)
    except Exception:
        return None


def items_of(data):
    if isinstance(data, dict):
        for key in ("data", "content", "items", "list"):
            value = data.get(key)
            if isinstance(value, list):
                return value
    if isinstance(data, list):
        return data
    return []


def find_upload_url(node):
    if isinstance(node, dict):
        url = node.get("upload_url")
        if isinstance(url, str) and url:
            return url
        for value in node.values():
            found = find_upload_url(value)
            if found:
                return found
    elif isinstance(node, list):
        for value in node:
            found = find_upload_url(value)
            if found:
                return found
    return ""


def main():
    if len(sys.argv) < 2:
        print("")
        return
    mode = sys.argv[1]
    data = load(sys.stdin)

    if mode == "id":
        tag = sys.argv[2] if len(sys.argv) > 2 else ""
        for item in items_of(data):
            if isinstance(item, dict) and item.get("tag_name") == tag:
                print(item.get("id", ""))
                return
        print("")
    elif mode == "upload_url":
        print(find_upload_url(data))
    elif mode == "json":
        print(json.dumps(data, ensure_ascii=False)[:400])
    else:
        print("")


if __name__ == "__main__":
    main()
