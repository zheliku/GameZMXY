using GameLogic.Architecture;
using System;
using GameFramework.Event;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic;
using GameLogic.Level;
using GameLogic.Save;
using GameLogic.Profile;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI
{
    /// <summary>
    /// 地图界面：显示旧项目地点图片，以点击事件提交所选关卡。
    /// </summary>
    public partial class MapForm : IController
    {
        private bool m_IsBound; // 当前打开周期拥有保存状态订阅，关闭后不再借用架构。

        /// <summary>
        /// 由所属流程处理 TitleRequested；参数为本次窗口编号。
        /// </summary>
        public event Action<int> TitleRequested;

        /// <summary>
        /// 由所属流程处理 SaveRequested；参数为本次窗口编号。
        /// </summary>
        public event Action<int> SaveRequested;

        /// <summary>
        /// 由所属流程处理 LevelSelected；参数为本次窗口编号及选择编号。
        /// </summary>
        public event Action<int, int> LevelSelected;

        /// <summary>
        /// 设置框架字段；按钮事件只在新建实例时绑定一次。
        /// </summary>
        /// <param name="serialId">窗口打开编号。</param>
        /// <param name="uiFormAssetName">窗口资源路径。</param>
        /// <param name="uiGroup">所属界面组。</param>
        /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
        /// <param name="isNewInstance">是否首次创建节点。</param>
        /// <param name="userData">本次打开参数。</param>
        public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
            bool isNewInstance, object userData)
        {
            m_SerialId = serialId;
            m_UIFormAssetName = uiFormAssetName;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = pauseCoveredUIForm;
            UIStringKeys.ForEach(key => key.SetLocalizationValue());
            if (isNewInstance)
            {
                m_Back.Pressed += OnBackClicked;
                m_Save.Pressed += OnSaveClicked;
                m_Huaguoshan.Pressed += OnHuaguoshanClicked;
                m_Shuiliandong.Pressed += OnShuiliandongClicked;
                m_Taohuayuan.Pressed += OnTaohuayuanClicked;
                m_Bianfuku.Pressed += OnBianfukuClicked;
                m_Qixingdong.Pressed += OnQixingdongClicked;
                m_Jialuoluo.Pressed += OnJialuoluoClicked;
                m_Qianan.Pressed += OnQiananClicked;
                m_Donghai.Pressed += OnDonghaiClicked;
                m_Shazhiwoliu.Pressed += OnShazhiwoliuClicked;
                m_Niumogu.Pressed += OnNiumoguClicked;
                m_Huaguoshan.MouseEntered += OnLocationMouseEntered;
                m_Huaguoshan.MouseExited += OnLocationMouseExited;
                m_Shuiliandong.MouseEntered += OnShuiliandongMouseEntered;
                m_Shuiliandong.MouseExited += OnLocationMouseExited;
                m_Taohuayuan.MouseEntered += OnTaohuayuanMouseEntered;
                m_Taohuayuan.MouseExited += OnLocationMouseExited;
                m_Bianfuku.MouseEntered += OnBianfukuMouseEntered;
                m_Bianfuku.MouseExited += OnLocationMouseExited;
                m_Qixingdong.MouseEntered += OnQixingdongMouseEntered;
                m_Qixingdong.MouseExited += OnLocationMouseExited;
                m_Jialuoluo.MouseEntered += OnJialuoluoMouseEntered;
                m_Jialuoluo.MouseExited += OnLocationMouseExited;
                m_Qianan.MouseEntered += OnQiananMouseEntered;
                m_Qianan.MouseExited += OnLocationMouseExited;
                m_Donghai.MouseEntered += OnDonghaiMouseEntered;
                m_Donghai.MouseExited += OnLocationMouseExited;
                m_Shazhiwoliu.MouseEntered += OnShazhiwoliuMouseEntered;
                m_Shazhiwoliu.MouseExited += OnLocationMouseExited;
                m_Niumogu.MouseEntered += OnNiumoguMouseEntered;
                m_Niumogu.MouseExited += OnLocationMouseExited;
            }
        }

        /// <summary>
        /// 绑定本次状态源并立即刷新页面。
        /// </summary>
        /// <param name="userData">本次窗口打开参数。</param>
        public void OnOpen(object userData)
        {
            Unbind();
            m_IsBound = true;
            try
            {
                this.GetModel<SaveModel>().ManualSave.RegisterWithInitValue(RefreshManualSave);
                this.GetModel<ProfileModel>().HighestClearedLevelId.RegisterWithInitValue(RefreshLevelProgress);
            }
            catch
            {
                Unbind();
                throw;
            }
            m_Save.Disabled = false;
            m_Save.MouseDefaultCursorShape = CursorShape.PointingHand;
            m_Save.SelfModulate = Colors.White;
            ResetLocationHover();
            PlayCurrentLocationHint();
            Visible = true;
        }

        /// <summary>
        /// 释放本次状态源引用，保留可复用的窗口节点。
        /// </summary>
        /// <param name="isShutdown">是否框架关停。</param>
        /// <param name="userData">关闭参数，不保存。</param>
        public void OnClose(bool isShutdown, object userData)
        {
            TitleRequested = null;
            SaveRequested = null;
            LevelSelected = null;
            Unbind();
            if (IsInstanceValid(m_LocationAnimation))
            {
                ResetLocationHover();
            }

            if (!isShutdown && IsInstanceValid(this))
            {
                Visible = false;
            }
        }

        /// <summary>
        /// 回池时清除本次打开的引用和框架编号。
        /// </summary>
        public void OnRecycle()
        {
            Unbind();
            if (IsInstanceValid(m_LocationAnimation))
            {
                ResetLocationHover();
            }

            m_SerialId = 0;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = true;
            if (IsInstanceValid(this))
            {
                Visible = false;
            }
        }

        /// <summary>
        /// 界面暂停；点击导航不依赖轮询。
        /// </summary>
        public void OnPause()
        {
        }

        /// <summary>
        /// 恢复界面暂停。
        /// </summary>
        public void OnResume()
        {
        }

        /// <summary>
        /// 窗口被覆盖。
        /// </summary>
        public void OnCover()
        {
        }

        /// <summary>
        /// 窗口恢复显示。
        /// </summary>
        public void OnReveal()
        {
        }

        /// <summary>
        /// 框架要求的更新入口；数值和导航通过事件更新。
        /// </summary>
        /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
        /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 读取所属游戏流程的架构入口。
        /// </summary>
        /// <returns>当前流程 FSM 的架构。</returns>
        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GF.Fsm.GetArchitecture();
        }

        /// <summary>
        /// 只在本周期仍拥有订阅时退订；回池后的重复清理不查询旧架构。
        /// </summary>
        private void Unbind()
        {
            if (!m_IsBound)
            {
                return;
            }

            m_IsBound = false;
            this.GetModel<SaveModel>().ManualSave.UnRegister(RefreshManualSave);
            this.GetModel<ProfileModel>().HighestClearedLevelId.UnRegister(RefreshLevelProgress);
        }

        /// <summary>
        /// 重新绑定显式传入的状态源。
        /// </summary>
        /// <param name="userData">新打开参数；null 保持当前绑定。</param>
        public void OnRefocus(object userData)
        {
            if (userData != null)
            {
                OnOpen(userData);
            }
        }

        /// <summary>
        /// 记录框架分配的窗口层级。
        /// </summary>
        /// <param name="uiGroupDepth">界面组深度。</param>
        /// <param name="depthInUIGroup">窗口在组内的深度。</param>
        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            m_DepthInUIGroup = depthInUIGroup;
            ZIndex = depthInUIGroup;
        }

        /// <summary>
        /// 返回主菜单。
        /// </summary>
        private void OnBackClicked()
        {
            if (!GF.UI.HasUIForm(SerialId))
            {
                return;
            }

            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
            TitleRequested?.Invoke(SerialId);
        }

        /// <summary>
        /// 请求流程提交当前档案，等待期间禁用重复点击。
        /// </summary>
        private void OnSaveClicked()
        {
            if (!GF.UI.HasUIForm(SerialId) || m_Save.Disabled)
            {
                return;
            }

            m_Save.Disabled = true;
            m_Save.MouseDefaultCursorShape = CursorShape.Arrow;
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
            SaveRequested?.Invoke(SerialId);
        }

        /// <summary>
        /// 显示属于本窗口的保存阶段和结果。
        /// </summary>
        /// <param name="result">模型已提交的保存状态。</param>
        private void RefreshManualSave(ManualSaveState result)
        {
            if (!GF.UI.HasUIForm(SerialId) || result.FormSerialId != SerialId)
            {
                return;
            }

            m_Save.Disabled = result.Phase == SaveWritePhase.Writing;
            m_Save.MouseDefaultCursorShape = m_Save.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            if (m_Save.Disabled)
            {
                return;
            }

            m_Save.SelfModulate = result.Phase == SaveWritePhase.Succeeded ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f);
        }

        /// <summary>
        /// 提交花果山的配置主键。
        /// </summary>
        private void OnHuaguoshanClicked()
        {
            SelectLevel(1);
        }

        /// <summary>
        /// 提交水帘洞的配置主键。
        /// </summary>
        private void OnShuiliandongClicked()
        {
            SelectLevel(2);
        }

        /// <summary>
        /// 提交桃花源的配置主键。
        /// </summary>
        private void OnTaohuayuanClicked()
        {
            SelectLevel(3);
        }

        /// <summary>
        /// 提交蝙蝠窟的配置主键。
        /// </summary>
        private void OnBianfukuClicked()
        {
            SelectLevel(4);
        }

        /// <summary>
        /// 提交七星洞的配置主键。
        /// </summary>
        private void OnQixingdongClicked()
        {
            SelectLevel(5);
        }

        /// <summary>
        /// 提交迦楼罗之穴的配置主键。
        /// </summary>
        private void OnJialuoluoClicked()
        {
            SelectLevel(6);
        }

        /// <summary>
        /// 提交浅岸的配置主键。
        /// </summary>
        private void OnQiananClicked()
        {
            SelectLevel(7);
        }

        /// <summary>
        /// 提交东海的配置主键。
        /// </summary>
        private void OnDonghaiClicked()
        {
            SelectLevel(8);
        }

        /// <summary>
        /// 提交鲨之涡流的配置主键。
        /// </summary>
        private void OnShazhiwoliuClicked()
        {
            SelectLevel(9);
        }

        /// <summary>
        /// 请求进入牛魔谷。
        /// </summary>
        private void OnNiumoguClicked()
        {
            SelectLevel(10);
        }

        /// <summary>
        /// 由地图流程处理已有配置的关卡请求。
        /// </summary>
        /// <param name="levelId">地图地点对应的关卡编号。</param>
        private void SelectLevel(int levelId)
        {
            if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
            {
                return;
            }

            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
            LevelSelected?.Invoke(SerialId, levelId);
        }

        /// <summary>
        /// 悬停时轻微放大，并循环闪烁原图中的黄色光晕。
        /// </summary>
        private void OnLocationMouseEntered()
        {
            PlayLocationHover(1, LocationAnimations.HuaguoshanHover);
        }

        /// <summary>
        /// 播放水帘洞的原图轮廓悬停。
        /// </summary>
        private void OnShuiliandongMouseEntered()
        {
            PlayLocationHover(2, LocationAnimations.ShuiliandongHover);
        }

        /// <summary>
        /// 播放桃花源的原图轮廓悬停。
        /// </summary>
        private void OnTaohuayuanMouseEntered()
        {
            PlayLocationHover(3, LocationAnimations.TaohuayuanHover);
        }

        /// <summary>
        /// 播放蝙蝠窟的原图轮廓悬停。
        /// </summary>
        private void OnBianfukuMouseEntered()
        {
            PlayLocationHover(4, LocationAnimations.BianfukuHover);
        }

        /// <summary>
        /// 播放七星洞的原图轮廓悬停。
        /// </summary>
        private void OnQixingdongMouseEntered()
        {
            PlayLocationHover(5, LocationAnimations.QixingdongHover);
        }

        /// <summary>
        /// 播放迦楼罗之穴的原图轮廓悬停。
        /// </summary>
        private void OnJialuoluoMouseEntered()
        {
            PlayLocationHover(6, LocationAnimations.JialuoluoHover);
        }

        /// <summary>
        /// 播放浅岸的原图轮廓悬停。
        /// </summary>
        private void OnQiananMouseEntered()
        {
            PlayLocationHover(7, LocationAnimations.QiananHover);
        }

        /// <summary>
        /// 播放东海原图的悬停光晕。
        /// </summary>
        private void OnDonghaiMouseEntered()
        {
            PlayLocationHover(8, LocationAnimations.DonghaiHover);
        }

        /// <summary>
        /// 播放鲨之涡流原图的悬停光晕。
        /// </summary>
        private void OnShazhiwoliuMouseEntered()
        {
            PlayLocationHover(9, LocationAnimations.ShazhiwoliuHover);
        }

        /// <summary>
        /// 播放牛魔谷原图的悬停光晕。
        /// </summary>
        private void OnNiumoguMouseEntered()
        {
            PlayLocationHover(10, LocationAnimations.NiumoguHover);
        }

        /// <summary>
        /// 只有当前地点放大，其他地点恢复原来的尺寸和命中轮廓。
        /// </summary>
        /// <param name="animation">原生地点悬停动画。</param>
        /// <param name="levelId">地点对应的关卡，锁定时忽略迟到信号。</param>
        private void PlayLocationHover(int levelId, StringName animation)
        {
            if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
            {
                return;
            }

            ResetLocationHover();
            m_LocationAnimation.Play(animation);
            m_LocationAnimation.Advance(0);
        }

        /// <summary>
        /// 鼠标离开后继续显示最新开放关卡的边缘闪烁。
        /// </summary>
        private void OnLocationMouseExited()
        {
            ResetLocationHover();
            if (GF.UI.HasUIForm(SerialId))
            {
                PlayCurrentLocationHint();
            }
            else
            {
                ResetLocationHover();
            }
        }

        /// <summary>
        /// 只闪烁本档案最新开放且已实现的地点，历史关卡仍可悬停和重玩。
        /// </summary>
        private void PlayCurrentLocationHint()
        {
            StringName animation = this.GetSystem<ProfileSystem>().LatestLevelId switch
            {
                1 => LocationAnimations.HuaguoshanCurrent,
                2 => LocationAnimations.ShuiliandongCurrent,
                3 => LocationAnimations.TaohuayuanCurrent,
                4 => LocationAnimations.BianfukuCurrent,
                5 => LocationAnimations.QixingdongCurrent,
                6 => LocationAnimations.JialuoluoCurrent,
                7 => LocationAnimations.QiananCurrent,
                8 => LocationAnimations.DonghaiCurrent,
                9 => LocationAnimations.ShazhiwoliuCurrent,
                10 => LocationAnimations.NiumoguCurrent,
                _ => throw new InvalidOperationException("最新开放关卡缺少地图动画。")
            };
            m_LocationAnimation.Play(animation);
            m_LocationAnimation.Advance(0);
        }

        /// <summary>
        /// 进度改变或窗口重开时同步可交互地点，并清除旧提示。
        /// </summary>
        /// <param name="highestClearedLevelId">本次通知的最远通关记录。</param>
        private void RefreshLevelProgress(int highestClearedLevelId)
        {
            foreach (var (button, id) in new[] { (m_Huaguoshan, 1), (m_Shuiliandong, 2), (m_Taohuayuan, 3), (m_Bianfuku, 4), (m_Qixingdong, 5), (m_Jialuoluo, 6), (m_Qianan, 7), (m_Donghai, 8), (m_Shazhiwoliu, 9), (m_Niumogu, 10) })
            {
                button.Disabled = !this.GetSystem<ProfileSystem>().CanEnterLevel(id);
                button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            }
            ResetLocationHover();
            PlayCurrentLocationHint();
        }

        /// <summary>
        /// 清除悬停参数，窗口复用时从普通图像开始。
        /// </summary>
        private void ResetLocationHover()
        {
            foreach (StringName animation in new[]
            {
            LocationAnimations.HuaguoshanReset,
            LocationAnimations.ShuiliandongReset,
            LocationAnimations.TaohuayuanReset,
            LocationAnimations.BianfukuReset,
            LocationAnimations.QixingdongReset,
            LocationAnimations.JialuoluoReset,
            LocationAnimations.QiananReset,
            LocationAnimations.DonghaiReset,
            LocationAnimations.ShazhiwoliuReset,
            LocationAnimations.NiumoguReset,
        })
            {
                m_LocationAnimation.Play(animation);
                m_LocationAnimation.Advance(0);
            }
            m_LocationAnimation.Stop();
        }

        /// <summary>
        /// 地图各地点普通、悬停与当前关卡提示的原生动画键。
        /// </summary>
        private static class LocationAnimations
        {
            public static readonly StringName HuaguoshanHover = "location/hover";
            public static readonly StringName ShuiliandongHover = "shuiliandong/hover";
            public static readonly StringName TaohuayuanHover = "taohuayuan/hover";
            public static readonly StringName BianfukuHover = "bianfuku/hover";
            public static readonly StringName QixingdongHover = "qixingdong/hover";
            public static readonly StringName JialuoluoHover = "jialuoluo/hover";
            public static readonly StringName QiananHover = "qianan/hover";
            public static readonly StringName DonghaiHover = "donghai/hover";
            public static readonly StringName ShazhiwoliuHover = "shazhiwoliu/hover";
            public static readonly StringName NiumoguHover = "niumogu/hover";

            public static readonly StringName HuaguoshanCurrent = "location/current";
            public static readonly StringName ShuiliandongCurrent = "shuiliandong/current";
            public static readonly StringName TaohuayuanCurrent = "taohuayuan/current";
            public static readonly StringName BianfukuCurrent = "bianfuku/current";
            public static readonly StringName QixingdongCurrent = "qixingdong/current";
            public static readonly StringName JialuoluoCurrent = "jialuoluo/current";
            public static readonly StringName QiananCurrent = "qianan/current";
            public static readonly StringName DonghaiCurrent = "donghai/current";
            public static readonly StringName ShazhiwoliuCurrent = "shazhiwoliu/current";
            public static readonly StringName NiumoguCurrent = "niumogu/current";

            public static readonly StringName HuaguoshanReset = "location/RESET";
            public static readonly StringName ShuiliandongReset = "shuiliandong/RESET";
            public static readonly StringName TaohuayuanReset = "taohuayuan/RESET";
            public static readonly StringName BianfukuReset = "bianfuku/RESET";
            public static readonly StringName QixingdongReset = "qixingdong/RESET";
            public static readonly StringName JialuoluoReset = "jialuoluo/RESET";
            public static readonly StringName QiananReset = "qianan/RESET";
            public static readonly StringName DonghaiReset = "donghai/RESET";
            public static readonly StringName ShazhiwoliuReset = "shazhiwoliu/RESET";
            public static readonly StringName NiumoguReset = "niumogu/RESET";
        }
    }
}
