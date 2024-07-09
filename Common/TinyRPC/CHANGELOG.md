# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [1.0.0] - 2024-05-24

first release 

## [2.0.0] - 2024-07-10

### Fixed

- 解决了切换生成代码存储位置时不能正确触发导入和编译的异常

### Added

* 网络发现新增超时事件

* 将 .proto 文件加入 IDE 资源列表，方便编辑

* 支持将指定 .asmdef 作为生成代码的引用

* 支持指定 Common 消息类型设置为 partial class 而不是默认的 struct

### Changed

- 优化了生成代码存储位置的切换逻辑
- proto 文件支持直接从设置面板 ReorderableList “+” 按钮新建
- Ping 消息使用非主线程处理，从代码层面避免了 ping 值虚高的问题
- proto 文件现在与生成的代码存于同一个 package，方便一处修改多处同步
