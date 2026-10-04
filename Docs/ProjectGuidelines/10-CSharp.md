# 10 C# 与文档

## 命名

| 成员                         | 规则                                       |
| ---------------------------- | ------------------------------------------ |
| 类型、枚举、方法、属性、事件 | `PascalCase`；接口以 `I` 开头          |
| 字段（含`[Export]`）       | `m_` + `PascalCase`，如 `m_Hp`       |
| 参数、局部变量               | `camelCase`                              |
| 常量                         | `PascalCase`；优先用有名常量表达工程规则 |
| 命名空间                     | `GameLogic.<领域>` 或 `GameConfig`     |

一文件一个主要类型，文件名与类型名一致。标识符使用英文；中文只用于配置文本和用户可见内容。

## XML 文档

- 手写 C# 的类型、字段、属性、事件、构造函数和方法都写 XML 文档注释；覆盖私有成员、重写方法和 Godot 生命周期回调。
- `<summary>` 用一句话说明职责。参数、返回值、异常或单位会影响调用方式时，补 `<param>`、`<returns>`、`<exception>`。
- 文档写契约：作用、单位、生命周期、所有权或限制；不要复述方法体，也不要写空泛注释。
- 局部变量不单独写文档。非直观算法可用短注释说明决策原因。
- 生成代码不补注释；改对应源表、模板或手写逻辑。

```csharp
/// <summary>返回当前生命值。</summary>
public int Hp { get; private set; }

/// <summary>对目标应用一次攻击结算。</summary>
/// <param name="target">受击实体。</param>
/// <returns>最终伤害值。</returns>
public int ApplyHit(ActorEntity target)
{
    // ...
}
```

## 类型与运行时约定

- 生命、攻防等整数属性用 `int`；倍率、概率用 `float`；避免隐式截断。
- 日志通过 `Log.Debug/Info/Warning/Error/Fatal`；玩法代码不直接写控制台或 `GD.Print`。烟测协议输出例外。
- 使用框架池化接口的对象按所有权及时归还；同一实例不得重复归还。
- `Battle/` 的公式保持确定性；随机值由调用方传入。详细边界见 [30-EntitiesAndCombat.md](30-EntitiesAndCombat.md)。
