using Mafi;
using Mafi.Base;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Input;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.Cursors;
using Mafi.Unity.InputControl.Toolbar;
using Mafi.Unity.UiFramework;
using Mafi.Unity.UserInterface;
using System;
using UnityEngine;
using Mafi.Unity.UiFramework.Components;
using Mafi.Core.Entities;
using Mafi.Localization;
using Transport = Mafi.Core.Factory.Transports.Transport;
using Mafi.Unity.InputControl.Factory;
using System.Collections.Generic;

namespace COITweaks
{
    public class PillarTweaksController : IToolbarItemInputController, IUnityUi
    {
        private static Logger log = Logger.WithName("Pillar Tweaks");
        public enum Mode
        {
            ADD, REMOVE, TOGGLE
        }

        private ShortcutsManager m_shortcutsManager;
        private IUnityInputMgr m_inputManager;
        private CursorPickingManager m_picker;
        private CursorManager m_cursorManager;
        private ToolbarController m_toolbarController;
        private Predicate<Transport> m_transportMatcher;
        private TransportsManager m_transportsManager;
        private EntitiesManager m_entitiesManager;
        private TransportPillarsBuilder m_transportPillarsBuilder;
        private Cursoor m_cursor;
        private TransportTrajectoryHighlighter m_transportHighlighter;
        private InternalToolbox m_toolbox;


        internal readonly KeyBindings add = KeyBindings.FromKey(KbCategory.Tools, KeyCode.Keypad1);
        internal readonly KeyBindings remove = KeyBindings.FromKey(KbCategory.Tools, KeyCode.Keypad2);
        internal readonly KeyBindings toggle = KeyBindings.FromKey(KbCategory.Tools, KeyCode.Keypad3);
        internal Mode mode;
        private Tile3i? startPosition = null;
        private Transport m_selectedTransport = null;


        public PillarTweaksController(
          ShortcutsManager shortcutsManager,
          IUnityInputMgr inputManager,
          CursorPickingManager cursorPickingManager,
          CursorManager cursorManager,
          ToolbarController toolbarController,
          TransportsManager transportsManager,
          EntitiesManager entitiesManager,
          TransportPillarsBuilder pillarsBuilder,
          NewInstanceOf<TransportTrajectoryHighlighter> transportHighlighter)
        {
            this.m_shortcutsManager = shortcutsManager;
            this.m_inputManager = inputManager;
            this.m_picker = cursorPickingManager;
            this.m_cursorManager = cursorManager;
            this.m_toolbarController = toolbarController;
            this.m_transportMatcher = entity => !entity.IsDestroyed && entity is Transport;
            this.m_transportsManager = transportsManager;
            this.m_entitiesManager = entitiesManager;
            this.m_transportPillarsBuilder = pillarsBuilder;
            this.m_transportHighlighter = transportHighlighter.Instance;

            this.m_toolbox = new InternalToolbox(this, toolbarController);
        }

        private void SwitchMode(Mode mode)
        {
            this.mode = mode;
            ResetSelection();
        }

        public void RegisterUi(UiBuilder builder)
        {
            this.m_toolbarController.AddMainMenuButton("Pillar Tweaks", this, IconsPaths.ToolbarLandmarks, 1537f, _ => KeyBindings.FromKey(KbCategory.Tools, KeyCode.F9));
            this.m_cursor = this.m_cursorManager.RegisterCursor(builder.Style.Cursors.Delete);
            this.m_toolbox.RegisterUi(builder);
        }

        public void Activate()
        {
            log.Info("Activate Controller");
            this.m_cursor.Show();
            this.m_toolbox.Show();
            this.m_toolbox.Enable(this.m_toolbox.AddPillarBtn);
        }

        public void Deactivate()
        {
            log.Info("Deactivate Controller");
            ResetSelection();
            this.m_toolbox.Hide();
            this.m_cursor.Hide();
            this.m_picker.ClearPicked();
        }

        public bool InputUpdate(IInputScheduler inputScheduler)
        {

            if (this.m_shortcutsManager.IsOn(add))
            {
                m_toolbox.Enable(m_toolbox.AddPillarBtn);
            }
            else if (this.m_shortcutsManager.IsOn(remove))
            {
                m_toolbox.Enable(m_toolbox.RemovePillarBtn);
            }
            else if (this.m_shortcutsManager.IsOn(toggle))
            {
                m_toolbox.Enable(m_toolbox.TogglePillarBtn);
            }

            // Abort by secondary action
            if (this.m_shortcutsManager.IsSecondaryActionDown)
            {
                this.m_inputManager.DeactivateController(this);
                return true;
            }

            // Hover action - only requires an update if the hover actually changes
            Option<Transport> transportOpt = this.m_picker.PickEntity(this.m_transportMatcher);

            if (mode == Mode.TOGGLE)
            {
                // Toggle mode: Single selection, click to add / remove pillar, depending on if the tile has a pillar or not
                if (transportOpt.IsNone) return false;

                Transport transport = transportOpt.Value;
                Tile3i position = transport.GetClosestTransportPosition(this.m_picker.LastPickedCoord.Tile3i);

                HighlightSlice(transport, position, position, out var _);
                if (m_shortcutsManager.IsPrimaryActionUp)
                {
                    if (m_transportsManager.HasPillarAt(position.Tile2i, position.Height, out var pillar))
                    {
                        RemovePillar(pillar);
                    }
                    else
                    {
                        BuildPillarAt(transport, position);
                    }
                }
            }
            else
            {
                // Add / Remove mode
                // Select part of transport (by clicking on start and end)
                if (!startPosition.HasValue)
                {
                    // Start position still needs to be set:
                    // Hovering highlights only current tile, click sets start position
                    if (transportOpt.IsNone)
                    {
                        ResetSelection();
                        return false;
                    }

                    Transport transport = transportOpt.Value;
                    Tile3i position = transport.GetClosestTransportPosition(this.m_picker.LastPickedCoord.Tile3i);

                    SelectNewTransport(transport, position);
                }
                else
                {
                    // Start position was already set
                    // highlight everything from start to current hovered tile
                    // click triggers action (unless on different transport)
                    if (transportOpt.IsNone)
                    {
                        // Currently not hovering over anything, nothing to update
                        return false;
                    }

                    Transport transport = transportOpt.Value;
                    Tile3i position = transport.GetClosestTransportPosition(this.m_picker.LastPickedCoord.Tile3i);

                    if (transport != m_selectedTransport)
                    {
                        SelectNewTransport(transport, position);
                    }
                    else
                    {
                        HighlightSlice(transport, startPosition.Value, position, out var trajectory);
                        if (m_shortcutsManager.IsPrimaryActionUp)
                        {
                            ProcessTrajectory(transport, trajectory);
                            ResetSelection();
                        }
                    }

                }
            }
            return false;
        }

        private void SelectNewTransport(Transport transport, Tile3i position)
        {
            log.Info($"Select transport {transport} ({transport.Prototype}) at position {position}");
            if (m_shortcutsManager.IsPrimaryActionUp)
            {
                startPosition = position;
                m_selectedTransport = transport;
            }
            HighlightSlice(transport, position, position, out var _);
        }

        private void HighlightSlice(Transport transport, Tile3i start, Tile3i end, out TransportTrajectory sliceTrajectory)
        {
            log.Info($"Highlight slice from {start} to {end} on transport {transport} ({transport.Prototype})");
            SliceTrajectory(transport, start, end, out sliceTrajectory);
            if (sliceTrajectory != null)
            {
                this.m_transportHighlighter.ClearAllHighlights();
                this.m_transportHighlighter.HighlightTrajectory(sliceTrajectory, ColorRgba.Cyan);
            }
        }

        private void ResetSelection()
        {
            log.Info("Reset Selection");

            // Reset values
            startPosition = null;
            this.m_selectedTransport = null;
            this.m_transportHighlighter.ClearAllHighlights();
        }


        // Since Trajectory.CanCutOut is a huge method performing checks and cutting at the same time, it's unusable for this case.
        private bool SliceTrajectory(Transport transport, Tile3i start, Tile3i end, out TransportTrajectory sliceTrajectory)
        {
            sliceTrajectory = null;
            if (m_selectedTransport != null && transport != m_selectedTransport)
            {
                log.Warn($"SliceTrajectory: Start {start} and end {end} are on different transports: {m_selectedTransport} vs. {transport}. Aborting.");
                return false;
            }
            var trajectory = transport.Trajectory;
            var pivots = transport.Trajectory.Pivots;

            log.Info($"SliceTrajectory: Create slice from {start} to {end} for transport {transport}");
            log.Info($"SliceTrajectory: Transport trajectory pivots: {String.Join(", ", pivots.ToArray())}");

            // returns the last pivot where the value is contained 
            if (!trajectory.TryGetLowPivotIndexFor(start, out var startIndex, out var startIsAtPivot))
            {
                Log.Warning($"Cannot cut transport from position {start} that is not on the transport.");
                return false;
            }

            if (!trajectory.TryGetLowPivotIndexFor(end, out var endIndex, out var endIsAtPivot))
            {
                Log.Warning($"Cannot cut transport to position {end} that is not on the transport.");
                return false;
            }

            if (endIndex < startIndex || (endIndex == startIndex && pivots[startIndex].DistanceSqrTo(start) > pivots[endIndex].DistanceSqrTo(end)))
            {
                Swap.Them(ref startIndex, ref endIndex);
                Swap.Them(ref start, ref end);
                Swap.Them(ref startIsAtPivot, ref endIsAtPivot);
            }

            if (start == end)
            {
                RelTile3i startDirection;
                RelTile3i endDirection;
                if (startIsAtPivot)
                {
                    // Pivot selected
                    startDirection = trajectory.StartDirectionOf(startIndex);
                    endDirection = trajectory.EndDirectionOf(startIndex);
                }
                else
                {
                    startDirection = trajectory.EndDirectionOf(startIndex);
                    endDirection = trajectory.StartDirectionOf(startIndex + 1);
                }
                // Single tile selection, easier use-case
                TransportTrajectory.TryCreateFromPivots(transport.Prototype, ImmutableArray.Create(start), startDirection, endDirection, out sliceTrajectory, out _);
                return true;
            }

            List<Tile3i> slice = new List<Tile3i>(endIndex - startIndex + 3);
            slice.Add(start);
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                slice.Add(pivots[i]);
            }
            if (!endIsAtPivot) slice.Add(end);

            log.Info($"SliceTrajectory: Determined Slice: {String.Join(", ", slice)}");

            bool result = TransportTrajectory.TryCreateFromPivots(transport.Prototype, ImmutableArray.ToImmutableArray(slice), null, null, out sliceTrajectory, out var error);

            log.Info($"SliceTrajectory: Created trajectory: {sliceTrajectory}");
            log.ErrorIf(result, error);
            return result;
        }

        private bool ProcessTrajectory(Transport transport, TransportTrajectory trajectory)
        {
            if (m_toolbox.AddPillarBtn.IsOn)
            {
                BuildPillarAlongTrajectory(transport, trajectory);
            }
            else if (m_toolbox.RemovePillarBtn.IsOn)
            {
                RemovePillarAlongTrajectory(transport, trajectory);
            }

            return true;
        }

        private bool BuildPillarAlongTrajectory(Transport transport, TransportTrajectory trajectory)
        {
            foreach (var tile in trajectory.OccupiedTiles)
            {
                if (!m_transportsManager.CanBuildPillarAt(tile.Position, tile.From, out var pillarBaseZ, out var pillarHeight))
                {
                    log.Info($"Cannot build pillar at {tile.Position} to transport base {tile.From}. (Computed pillar base {pillarBaseZ}, pillar height {pillarHeight}");
                    continue;
                }

                var pillarPosition = tile.Position.ExtendHeight(pillarBaseZ);
                TransportPillar pillar = m_transportPillarsBuilder.Create(pillarPosition, pillarHeight);
                m_entitiesManager.AddEntityNoChecks(pillar);
                log.Info($"Added new pillar at {pillarPosition} with height {pillarHeight}");
            }
            return true;
        }

        private bool BuildPillarAt(Transport transport, Tile3i position)
        {

            if (!m_transportsManager.CanBuildPillarAt(position.Tile2i, position.Height, out var pillarBaseZ, out var pillarHeight))
            {
                log.Info($"Cannot build pillar. (Computed pillar base {pillarBaseZ}, pillar height {pillarHeight}");
                return false;
            }

            var pillarPosition = position.Tile2i.ExtendHeight(pillarBaseZ); // should be same as position, but eh. who knows.
            TransportPillar pillar = m_transportPillarsBuilder.Create(pillarPosition, pillarHeight);
            m_entitiesManager.AddEntityNoChecks(pillar);
            log.Info($"Added new pillar at {pillarPosition} with height {pillarHeight}");
            return true;
        }

        private bool RemovePillar(TransportPillar pillar)
        {
            log.Info($"Remove pillar {pillar}: pos: {pillar.CenterTile}, height: {pillar.Height}, top height: {pillar.TopTileHeight}");
            m_entitiesManager.RemoveAndDestroyEntityNoChecks(pillar, EntityRemoveReason.Remove);
            return true;
        }

        private bool RemovePillarAlongTrajectory(Transport transport, TransportTrajectory trajectory)
        {
            foreach (var tile in trajectory.OccupiedTiles)
            {
                if (m_transportsManager.HasPillarAt(tile.Position, tile.From, out var pillar))
                {
                    log.Info($"Remove pillar {pillar}: pos: {pillar.CenterTile}, height: {pillar.Height}, top height: {pillar.TopTileHeight}");
                    m_entitiesManager.RemoveAndDestroyEntityNoChecks(pillar, EntityRemoveReason.Remove);
                }
            }

            return true;
        }

        public bool IsVisible => true;

        public ControllerConfig Config => ControllerConfig.GameMenu;

        public event Action<IToolbarItemInputController> VisibilityChanged;

        private class InternalToolbox : Toolbox, IUnityUi
        {
            private readonly PillarTweaksController m_controller;

            public ToggleBtn AddPillarBtn;
            public ToggleBtn RemovePillarBtn;
            public ToggleBtn TogglePillarBtn;

            LocStr AddPillarTooltip = Loc.Str("AddPillar__Tooltip", "Click to add pillars to a section (select start- and end-point)", "tooltip");
            LocStr RemovePillarTooltip = Loc.Str("RemovePillar__Tooltip", "Click to remove pillars from a section (select start- and end-point)", "tooltip");
            LocStr TogglePillarTooltip = Loc.Str("TogglePillar__Tooltip", "Click to add/remove a pillar at that tile", "tooltip");

            Dictionary<ToggleBtn, Mode> dict = new Dictionary<ToggleBtn, Mode>();


            public InternalToolbox(PillarTweaksController controller, ToolbarController toolbar) : base(toolbar)
            {
                m_controller = controller;
            }

            protected override void BuildCustomItems(UiBuilder builder)
            {
                this.AddPillarBtn = this.AddToggleButton("Add Pillar", IconsPaths.ToolbarWaste, v => EnableIf(AddPillarBtn, v), m => m_controller.add, (LocStrFormatted)AddPillarTooltip);
                this.RemovePillarBtn = this.AddToggleButton("Remove Pillar", IconsPaths.ToolbarMetallurgy, v => EnableIf(RemovePillarBtn, v), m => m_controller.remove, (LocStrFormatted)RemovePillarTooltip);
                this.TogglePillarBtn = this.AddToggleButton("Toggle Pillar", IconsPaths.ToolbarMetallurgy, v => EnableIf(TogglePillarBtn, v), m => m_controller.toggle, (LocStrFormatted)TogglePillarTooltip);

                dict.Add(AddPillarBtn, Mode.ADD);
                dict.Add(RemovePillarBtn, Mode.REMOVE);
                dict.Add(TogglePillarBtn, Mode.TOGGLE);

                this.Toolbar.AddToolbox(this, this.GetWidth());
            }
            private void EnableIf(ToggleBtn toggle, bool condition)
            {
                if (condition) Enable(toggle);
            }

            internal void Enable(ToggleBtn toggle)
            {
                foreach (var btn in new[] { AddPillarBtn, RemovePillarBtn, TogglePillarBtn })
                {
                    if (btn == toggle) btn.SetIsOn(true);
                    else btn.SetIsOn(false);
                }
                m_controller.SwitchMode(dict[toggle]);
            }
        }
    }
}
