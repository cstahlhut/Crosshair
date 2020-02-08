using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Draygo.API;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.AI;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Input;
using VRageRender;
using IMyCockpit = Sandbox.ModAPI.IMyCockpit;
using IMyInput = VRage.ModAPI.IMyInput;
using IMyShipController = Sandbox.ModAPI.IMyShipController;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Crosshair
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Crosshair : MySessionComponentBase
    {
        private HudAPIv2 hudApIv2;
        private HudAPIv2.BillBoardHUDMessage crosshairIcon_Billboard_Message = null;
        private Logging _logging = new Logging("cross_hair.log");
        private static readonly MyStringId CrossHairIcon = MyStringId.GetOrCompute("CrossHairIcon");
        private int _tick = 0;
        private int _keypressWait = 0;
        private bool _initialized = false;
        private bool _hudInit = false;
        private bool _crosshairDisplayedPlayer = true;
        private bool _crosshairDisplayedCockpit = true;
        private bool _crosshairDisplayOnHudHidden = true;
        private bool _viewDisabledByUser = false;
        private bool _hudHidden = false;
        private bool _hightlightBlocks = true;

        private void Init()
        {
            hudApIv2 = new HudAPIv2();
            MyAPIGateway.Utilities.MessageEntered += ChatCommands;
            _logging.WriteLine("Cross hair mod initialized");
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (!_initialized)
                {
                    Init();
                    _initialized = true;
                }

                if (hudApIv2.Heartbeat == false && _tick >= 600)
                {
                    MyAPIGateway.Utilities.ShowNotification("HUDAPI MOD MISSING - PLEASE ENABLE", 5000, "Red");
                    _logging.WriteLine("Text HUD API was not detected!");
                }

                if (hudApIv2.Heartbeat && !_hudInit)
                {
                    crosshairIcon_Billboard_Message =
                        new HudAPIv2.BillBoardHUDMessage(CrossHairIcon, new Vector2D(0, 0), Color.White, null,
                            -1, 1D, 1F, 1F, 0F, true, true, BlendTypeEnum.PostPP);
                    crosshairIcon_Billboard_Message.Height = 1.80f;
                    crosshairIcon_Billboard_Message.Scale = 0.35d;
                    crosshairIcon_Billboard_Message.Rotation = 0f;
                    crosshairIcon_Billboard_Message.Material = CrossHairIcon;
                    _hudInit = true;
                    _logging.WriteLine("HUD initialised and heart beat active");
                }

                if (hudApIv2.Heartbeat && _hudInit)
                {
                    const int keyPressTimer = 1;
                    const MyKeys keyBind = MyKeys.F2;
                    var equippedItem = MyAPIGateway.Session?.Player?.Character?.Components?.Get<MyCharacterWeaponPositionComponent>();
                    var equippedTool = MyAPIGateway.Session?.Player?.Character?.EquippedTool;
                    var inCockpit = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity is IMyCockpit;

                    if ((equippedTool is IMyAngleGrinder || (equippedTool is IMyWelder)) && _hightlightBlocks)
                    {

                        var toolCast = equippedTool.Components?.Get<MyCasterComponent>();
                        var hitBlock =  (IMySlimBlock) toolCast?.HitBlock;
                        if (hitBlock != null)
                        {
                            var cubeBlockDefinition = hitBlock.BlockDefinition as MyCubeBlockDefinition;
                            Matrix result;
                            hitBlock.Orientation.GetMatrix(out result);
                            var matrixD = (MatrixD)result;
                            var worldMatrix1 = hitBlock.CubeGrid.Physics.GetWorldMatrix();
                            var worldMatrix2 = matrixD * Matrix.CreateTranslation((Vector3)hitBlock.Position) * Matrix.CreateScale(hitBlock.CubeGrid.GridSize) * worldMatrix1;
                            var lineWidth = hitBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.06f : 0.03f;
                            var vector1 = new Vector3(0.5f, 0.5f, 0.5f);
                            var vector2 = new Vector3(0.05f);
                            var localbox = new BoundingBoxD((Vector3D)((Vector3)(-cubeBlockDefinition.Center) - vector1 - vector2),
                                (Vector3D)((Vector3)(cubeBlockDefinition.Size - cubeBlockDefinition.Center) - vector1 + vector2));
                            var highlightColor = Color.White;
                            var highlightMaterial = MyStringId.GetOrCompute("");
                            if (equippedTool is IMyAngleGrinder)
                            {
                                highlightColor = Color.Red * 0.3f;
                                highlightMaterial = MyStringId.GetOrCompute("GizmoDrawLineRed");
                            }
                            if (equippedTool is IMyWelder)
                            {
                                highlightColor = Color.Green * 0.75f;
                                highlightMaterial = MyStringId.GetOrCompute("GizmoDrawLine");
                            }
                            MySimpleObjectDraw.DrawTransparentBox(ref worldMatrix2, ref localbox, ref highlightColor, MySimpleObjectRasterizer.Wireframe,
                                1, lineWidth, new MyStringId?(), highlightMaterial);
                        }
                    }

                    if (MyAPIGateway.Gui.ChatEntryVisible)
                    {
                        return;
                    }

                    if (MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    {
                        _hudHidden = MyAPIGateway.Session?.Config?.MinimalHud ?? false;
                    }

                    // When HUD is displayed
                    if (MyVisualScriptLogicProvider.IsNewKeyPressed(keyBind) && _crosshairDisplayedPlayer &&
                        _keypressWait == 0 && !_viewDisabledByUser && !_hudHidden)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = false;
                        _viewDisabledByUser = true;
                        _crosshairDisplayedPlayer = false;
                    }

                    if (MyVisualScriptLogicProvider.IsNewKeyPressed(keyBind) && !_crosshairDisplayedPlayer &&
                        _keypressWait == 0 && _viewDisabledByUser && !_hudHidden)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = true;
                        _viewDisabledByUser = false;
                        _crosshairDisplayedPlayer = true;
                    }

                    // When HUD is NOT displayed
                    if (MyVisualScriptLogicProvider.IsNewKeyPressed(keyBind) &&
                        !_crosshairDisplayOnHudHidden &&
                        _keypressWait == 0 && _hudHidden)
                    {
                        _keypressWait = keyPressTimer;
                        //MyVisualScriptLogicProvider.ShowNotification("Hide CrossHair on No HUD", 2000, "Red");
                        crosshairIcon_Billboard_Message.Visible = true;
                        crosshairIcon_Billboard_Message.Options |= HudAPIv2.Options.HideHud;
                        _crosshairDisplayOnHudHidden = true;

                    }

                    if (MyVisualScriptLogicProvider.IsNewKeyPressed(keyBind) &&
                        _crosshairDisplayOnHudHidden && _keypressWait == 0 && _hudHidden)
                    {
                        _keypressWait = keyPressTimer;
                        //MyVisualScriptLogicProvider.ShowNotification("Show CrossHair on No HUD", 2000, "Green");
                        crosshairIcon_Billboard_Message.Visible = true;
                        crosshairIcon_Billboard_Message.Options &= ~HudAPIv2.Options.HideHud;
                        _crosshairDisplayOnHudHidden = false;
                    }

                    // When player is in first view
                    if (MyAPIGateway.Session.Player.Character != null && MyAPIGateway.Session.Player.Character.IsInFirstPersonView && !_crosshairDisplayedPlayer &&
                        _keypressWait == 0 && !_viewDisabledByUser && !equippedItem.IsInIronSight)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = true;
                        _crosshairDisplayedPlayer = true;
                    }

                    // When player charecter is in 3rd person view
                    if (MyAPIGateway.Session.Player.Character != null && !MyAPIGateway.Session.Player.Character.IsInFirstPersonView && _crosshairDisplayedPlayer &&
                        _keypressWait == 0 && !equippedItem.IsInIronSight && !inCockpit)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = false;
                        _crosshairDisplayedPlayer = false;
                    }

                    // When player looking down weapon sights
                    if (equippedItem.IsInIronSight && _crosshairDisplayedPlayer && _keypressWait == 0 && !_viewDisabledByUser)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = false;
                        _crosshairDisplayedPlayer = false;
                    }

                    if (!equippedItem.IsInIronSight && !_crosshairDisplayedPlayer && _keypressWait == 0 &&
                        !_viewDisabledByUser && MyAPIGateway.Session.Player.Character.IsInFirstPersonView && !inCockpit)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = true;
                        _crosshairDisplayedPlayer = true;
                    }

                    // When player in menus
                    if (!MyAPIGateway.Gui.IsCursorVisible && _crosshairDisplayedPlayer && _keypressWait == 0)
                    {
                        crosshairIcon_Billboard_Message.Visible = true;
                    }

                    if (MyAPIGateway.Gui.IsCursorVisible && _crosshairDisplayedPlayer && _keypressWait == 0 &&
                        !_viewDisabledByUser)
                    {
                        crosshairIcon_Billboard_Message.Visible = false;
                    }
                    
                    // When player in Cockpit third person
                    if (inCockpit && !((IMyCockpit)MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity)
                            .IsInFirstPersonView && _crosshairDisplayedCockpit && _keypressWait == 0 && !equippedItem.IsInIronSight)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = false;
                        _crosshairDisplayedCockpit = true;
                    }

                    // When player in Cockpit first person
                    if (inCockpit && ((IMyCockpit)MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity)
                        .IsInFirstPersonView && !_crosshairDisplayedCockpit && _keypressWait == 0 && !_viewDisabledByUser && !equippedItem.IsInIronSight)
                    {
                        _keypressWait = keyPressTimer;
                        crosshairIcon_Billboard_Message.Visible = true;
                        _crosshairDisplayedCockpit = true;
                    }
                }

                if (_keypressWait > 0) _keypressWait--;
                    _tick++;
                }
            catch (Exception e)
            {
                _logging.WriteLine("Crosshair MOD Error" + e.ToString());
            }
        }

        private void ChatCommands(string MessageText, ref bool SendToOthers)
        {
            try
            {
                if (MessageText.Equals("/EBH") && _hudInit)
                {
                    SendToOthers = false;
                    MyVisualScriptLogicProvider.ShowNotification("Tool Block Highlight Enabled", 2000, "Green");
                    _hightlightBlocks = true;
                    return;
                }

                if (MessageText.Equals("/DBH") && _hudInit)
                {
                    SendToOthers = false;

                    MyVisualScriptLogicProvider.ShowNotification("Tool Block Highlight Disabled", 2000, "Red");
                    _hightlightBlocks = false;
                    return;
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage(e.ToString());
            }

        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= ChatCommands;
            _logging.Close();
        }
    }
}
