using System;
using System.Text;

namespace RemoteTech {
    public class ModuleRTAntenna : PartModule, IAntenna {
        public bool CanTarget { get { return Mode1DishRange != -1; } }

        public String Name { get { return part.partInfo.title; } }

        public Guid DishTarget {
            get { return RTAntennaTargetGuid; }
            set {
                RTAntennaTargetGuid = value;
                RTAntennaTarget = value.ToString();
                UpdateContext();
            }
        }

        public float DishRange { get { return IsRTActive && IsPowered ? Mode1DishRange : Mode0DishRange; } }

        public double DishFactor { get { return RTDishFactor; } }

        public float OmniRange { get { return IsRTActive && IsPowered ? Mode1OmniRange : Mode0OmniRange; } }

        public float Consumption { get { return IsRTActive && IsPowered ? Mode1EnergyCost : Mode0EnergyCost; } }

        public Vessel Vessel { get { return vessel; } }

        [KSPField(guiName = "Dish range")] public String GUI_DishRange;
        [KSPField(guiName = "Energy")] public String GUI_EnergyReq;
        [KSPField(guiName = "Omni range")] public String GUI_OmniRange;
        [KSPField(guiActive = true, guiName = "Antenna status")] public String GUI_Status;

        [KSPField] public bool CanToggle = true;

        [KSPField] public String Mode0Name = "Inactive";
        [KSPField] public String Mode1Name = "Activated";

        [KSPField] public String ActionMode0Name = "Retract";
        [KSPField] public String ActionMode1Name = "Extend";
        [KSPField] public String ActionToggleName = "Toggle";

        [KSPField] public float Mode0DishRange = 2000.0f;
        [KSPField] public float Mode1DishRange = 70000.0f;

        [KSPField] public float Mode0OmniRange = 2000.0f;
        [KSPField] public float Mode1OmniRange = 70000.0f;

        [KSPField] public float Mode0EnergyCost = 0.0f;
        [KSPField] public float Mode1EnergyCost = 0.00166667f;

        [KSPField(isPersistant = true)] public bool IsPowered = true;
        [KSPField(isPersistant = true)] public bool IsRTActive = false;
        [KSPField(isPersistant = true)] public bool IsRTAntenna = true;

        public Guid RTAntennaTargetGuid = Guid.Empty;
        [KSPField(isPersistant = true)] public String RTAntennaTarget = Guid.Empty.ToString();

        [KSPField(isPersistant = true)] public double RTDishFactor;
        [KSPField(isPersistant = true)] public float RTOmniRange;
        [KSPField(isPersistant = true)] public float RTDishRange;

        private Guid mRegisteredId;

        public override string GetInfo() {
            var info = new StringBuilder();
            if (Mode1OmniRange > 0) {
                info.Append("Omni range: ");
                info.Append(RTUtil.FormatSI(Mode0OmniRange, "m"));
                info.Append(" / ");
                info.AppendLine(RTUtil.FormatSI(Mode1OmniRange, "m"));
            }
            if (Mode1DishRange > 0) {
                info.Append("Dish range: ");
                info.Append(RTUtil.FormatSI(Mode0DishRange, "m"));
                info.Append(" / ");
                info.AppendLine(RTUtil.FormatSI(Mode1DishRange, "m"));
            }
            if (Mode1EnergyCost > 0) {
                info.Append("Energy req.: ");
                info.Append((Mode1EnergyCost*60).ToString("0.00") + "/min.");
            }
            return info.ToString();
        }

        public virtual void SetState(bool state) {
            IsRTActive = state;
            RTDishRange = IsRTActive ? Mode1DishRange : Mode0DishRange;
            RTOmniRange = IsRTActive ? Mode1OmniRange : Mode0OmniRange;
            Events["EventOpen"].guiActive = !IsRTActive;
            Events["EventOpen"].active = Events["EventOpen"].guiActive;
            Events["EventClose"].guiActive = IsRTActive && CanToggle;
            Events["EventClose"].active = Events["EventClose"].guiActive;
            UpdateContext();
        }

        [KSPEvent(name = "EventToggle", guiActive = false)]
        public void EventToggle() {
            if (IsRTActive) {
                EventClose();
            }
            else {
                EventOpen();
            }
        }

        [KSPEvent(name = "EventTarget", guiActive = false, guiName = "Target")]
        public void EventTarget() {
            RTCore.Instance.Gui.OpenAntennaConfig(this, vessel);
        }

        [KSPEvent(name = "EventOpen", guiActive = false)]
        public void EventOpen() {
            SetState(true);
        }

        [KSPEvent(name = "EventClose", guiActive = false)]
        public void EventClose() {
            if (CanToggle) {
                SetState(false);
            }
        }

        [KSPAction("ActionToggle", KSPActionGroup.None)]
        public void ActionToggle(KSPActionParam param) {
            EventToggle();
        }

        [KSPAction("ActionOpen", KSPActionGroup.None)]
        public void ActionOpen(KSPActionParam param) {
            EventOpen();
        }

        [KSPAction("ActionClose", KSPActionGroup.None)]
        public void ActionClose(KSPActionParam param) {
            EventClose();
        }

        public override void OnStart(StartState state) {
            Actions["ActionOpen"].guiName = ActionMode1Name;
            Actions["ActionOpen"].active = true;
            Actions["ActionClose"].guiName = ActionMode0Name;
            Actions["ActionClose"].active = CanToggle;
            Actions["ActionToggle"].guiName = ActionToggleName;
            Actions["ActionToggle"].active = CanToggle;

            if (RTCore.Instance != null) {
                Events["EventOpen"].guiName = ActionMode1Name;
                Events["EventClose"].guiName = ActionMode0Name;
                Events["EventToggle"].guiName = ActionToggleName;

                Fields["GUI_OmniRange"].guiActive = Mode1OmniRange > 0;
                Fields["GUI_DishRange"].guiActive = Mode1DishRange > 0;
                Fields["GUI_EnergyReq"].guiActive = Mode1EnergyCost > 0;
                Events["EventTarget"].guiActive = Mode1DishRange > 0;
                Events["EventTarget"].active = Events["EventTarget"].guiActive;

                try {
                    DishTarget = new Guid(RTAntennaTarget);
                }
                catch (FormatException) {
                    DishTarget = Guid.Empty;
                }
                RTDishFactor = Math.Cos(2*2*Math.PI/360);

                mRegisteredId = RTCore.Instance.Antennas.Register(vessel.id, this);
                GameEvents.onVesselWasModified.Add(OnVesselModified);
                GameEvents.onPartUndock.Add(OnPartUndock);
                SetState(IsRTActive);

                UpdateContext();
            }
        }

        private void UpdateContext() {
            GUI_Status = IsRTActive ? Mode1Name : Mode0Name;
            GUI_OmniRange = RTUtil.FormatSI(OmniRange, "m");
            GUI_DishRange = RTUtil.FormatSI(DishRange, "m");
            GUI_EnergyReq = (Consumption*60).ToString("0.00") + "/min";
            Events["EventTarget"].guiName = RTUtil.TargetName(DishTarget);
        }

        public void OnDestroy() {
            if (RTCore.Instance != null) {
                RTCore.Instance.Antennas.Unregister(mRegisteredId, this);
                GameEvents.onVesselWasModified.Remove(OnVesselModified);
                GameEvents.onPartUndock.Remove(OnPartUndock);
            }
        }

        public void OnPartUndock(Part p) {
            if (p.vessel == vessel) {
                OnVesselModified(p.vessel);
            }
        }

        public void OnVesselModified(Vessel v) {
            if (vessel == null || (mRegisteredId != vessel.id)) {
                RTCore.Instance.Antennas.Unregister(mRegisteredId, this);
                if (vessel != null) {
                    mRegisteredId = RTCore.Instance.Antennas.Register(vessel.id, this);
                }
            }
        }

        public override String ToString() {
            return "ModuleRTAntenna {" + DishTarget + ", " + DishRange + ", " + OmniRange + ", " +
                   Vessel.vesselName + "}";
        }
    }
}
