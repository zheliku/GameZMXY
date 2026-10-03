//------------------------------------------------------------
// 启动流程（LaunchProcedure）
// 游戏的入口流程，完成框架初始化、加载配置和数据表、创建实体组
//------------------------------------------------------------

using System.Collections.Generic;
using GameFramework;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 启动流程。检测框架组件是否正常
/// </summary>
public class ProcedureLaunch : ProcedureBase
{
    /// <summary>
    /// 进入流程。
    /// 执行所有初始化工作后立即切换到菜单流程。
    /// </summary>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        Log.FeiBi(Colors.Yellow);
        Log.Info("[ProcedureLaunch] 验证框架组件...");
        List<string> missing = new();
        Check("Base", GF.Base != null);
        Check("Event", GF.Event != null);
        Check("Fsm", GF.Fsm != null);
        Check("Setting", GF.Setting != null);
        Check("DataNode", GF.DataNode != null);
        Check("Resource", GF.Resource != null);
        Check("Entity", GF.Entity != null);
        Check("UI", GF.UI != null);
        Check("Sound", GF.Sound != null);
        Check("Localization", GF.Localization != null);
        Check("WebRequest", GF.WebRequest != null);
        Check("Download", GF.Download != null);
        Check("Scene", GF.Scene != null);
        Check("ObjectPool", GF.ObjectPool != null);

        if (missing.Count == 0)
        {
            Log.Info("[LaunchProcedure] 框架组件验证通过");
            ChangeState<ProcedureUpdate>(procedureOwner);
        }
        else
        {
            Log.Fatal("[LaunchProcedure] 框架组件 {0} 验证失败", string.Join(", ", missing));
        }

        void Check(string name, bool available)
        {
            if (!available)
            {
                missing.Add(name);
            }
        }
    }


    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}
