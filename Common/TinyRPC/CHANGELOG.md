# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [3.0.0] - 2024-11-22

> Runtime

* 新增网络发现示例新增超时事件演示逻辑

* 修正当选择 Assets 存储生成代码时，路径创建异常的bug

* 使用 IDisposable 利用 using 语法糖在逻辑完成后自动回收，完善消息回收逻辑

* 修正某些情况下软件进入后台后断线的问题

> Editor

* 解决绘制 reorderable list 时超索引的异常

* 新增一键同步 IDE 解决方案的功能，解决新增文件在 IDE 中不能及时同步的问题





## [2.0.0] - 2024-07-10

> Runtime

- 新增网络发现新增超时事件

> Editor

* 解决了切换生成代码存储位置时不能正确触发导入和编译的异常

* 支持将指定 .asmdef 作为生成代码的引用

*  支持指定 Common 消息类型设置为 partial class 而不是默认的 struct

* 优化了生成代码存储位置的切换逻辑

*  约定 .proto 文件与生成的代码存放同一位置，方便一处修改多处同步

*  .proto 文件支持直接从设置面板 ReorderableList “+” 按钮新建

*  .proto 文件加入 IDE 资源列表，方便编辑

*  Ping 消息使用非主线程处理，从代码层面避免了 ping 值虚高的问题

## [1.0.0] - 2024-05-24

first release 
