using UnityEngine;
namespace SalvageSquad
{
    public sealed class GateRow : MonoBehaviour
    {
        public TextMesh leftLabel, rightLabel;
        public TextMesh leftDetail, rightDetail;
        public Transform leftFrame, rightFrame;
        public Renderer leftPlate, rightPlate;
        public TextMesh[] leftStroke, rightStroke;
        public bool ReducedMotion;
        GateEffect left, right;
        float exitTime;
        float leftPulse, rightPulse;
        int leftHits, rightHits;
        int selectedSide=-1;
        MaterialPropertyBlock tint;
        bool singleLane;
        float laneX;
        Vector3 leftHome, rightHome;
        bool homesSaved;
        readonly Transform[] chargeFills=new Transform[2];
        readonly Transform[] chargeTracks=new Transform[2];
        Transform collectedFrame;
        public bool Resolved { get; private set; }
        public void Prepare(GateEffect a, GateEffect b, float startZ = 15, int lane = 2)
        {
            if(!homesSaved) { leftHome=leftFrame.localPosition;rightHome=rightFrame.localPosition;homesSaved=true; }
            singleLane=lane>=-1&&lane<=1;laneX=singleLane?lane*2.6f:0;
            left = a; right = b; Resolved = false;selectedSide=-1;
            leftHits=rightHits=0;
            PrepareChargeBars();
            UpdateChoice(0); UpdateChoice(1);
            leftPulse = rightPulse = 0;
            leftHits = rightHits = 0;
            leftLabel.transform.localScale = rightLabel.transform.localScale = Vector3.one;
            leftFrame.gameObject.SetActive(true); rightFrame.gameObject.SetActive(!singleLane);
            // A fixed central gap makes left/right commitment visible throughout the approach.
            leftFrame.localPosition=singleLane?new Vector3(laneX,leftHome.y,leftHome.z):leftHome+Vector3.right*.3f;
            rightFrame.localPosition=rightHome-Vector3.right*.3f;
            leftFrame.localScale=new Vector3(singleLane?.67f:.5f,1,1);
            rightFrame.localScale=new Vector3(.5f,1,1);
            leftLabel.color=rightLabel.color=Color.white;
            exitTime = 0;
            transform.position = new Vector3(0, 0, startZ);
            gameObject.SetActive(true);
        }
        public static int Choice(float centerX) => centerX<=-1.1f?0:centerX>=1.1f?1:-1;
        // Same swept interval as bullets/enemies, including this row's forward scroll.
        public bool ShotCrossing(float x, float startZ, float endZ, float dt, out float fraction, float aimX=float.NaN)
        {
            fraction = float.PositiveInfinity;
            if (!gameObject.activeSelf || Resolved) return false;
            var effect = singleLane || x < 0 ? left : right;
            var frame=singleLane || x<0?leftFrame:rightFrame;
            float center = singleLane ? laneX : frame.position.x;
            if(!float.IsNaN(aimX)&&(singleLane?Mathf.Abs(aimX-laneX)>1.15f:Choice(aimX)!=(x<0?0:1)))return false;
            if (!effect.CanCharge || Mathf.Abs(x - center) > (singleLane?1.05f:1.5f*frame.localScale.x)) return false;
            float start = startZ - transform.position.z, end = endZ - (transform.position.z - 2.5f * dt);
            if (start > 0 || end < 0 || end <= start) return false;
            fraction = -start / (end - start);
            return true;
        }
        public bool Charge(float x)
        {
            if (Resolved || !gameObject.activeSelf || (singleLane&&Mathf.Abs(x-laneX)>1.05f)) return false;
            int side = singleLane?0:x<0?0:1;
            var frame=side==0?leftFrame:rightFrame;
            if(Mathf.Abs(x-frame.position.x)>(singleLane?1.05f:1.5f*frame.localScale.x))return false;
            var effect = side == 0 ? left : right;
            if (!effect.CanCharge) return false;
            // Each hit responds, including partial progress towards the next recruit.
            if(side==0)leftPulse=Mathf.Max(leftPulse,.35f);else rightPulse=Mathf.Max(rightPulse,.35f);
            if (side == 0) { if (++leftHits < Mathf.Max(1, effect.shotsPerIncrement)) { UpdateChargeBar(side);return false; } leftHits = 0; }
            else { if (++rightHits < Mathf.Max(1, effect.shotsPerIncrement)) { UpdateChargeBar(side);return false; } rightHits = 0; }
            bool wasPositive=effect.IsPositive;
            effect.value = effect.uncapped ? effect.value + effect.ChargeStep : Mathf.Min(effect.maxValue, effect.value + effect.ChargeStep);
            if (side == 0) { left = effect; leftPulse = 1; } else { right = effect; rightPulse = 1; }
            UpdateChoice(side);
            return !wasPositive&&effect.IsPositive;
        }
        void UpdateChoice(int side)
        {
            var effect = side == 0 ? left : right;
            var label = side == 0 ? leftLabel : rightLabel;
            var strokes = side == 0 ? leftStroke : rightStroke;
            var plate = side == 0 ? leftPlate : rightPlate;
            string value = effect.kind == GateKind.Damage || effect.kind == GateKind.FireRate ? "×" + effect.value.ToString("0.#") : effect.Label;
            label.text = value; label.characterSize = .15f*Mathf.Min(1,3.5f/value.Length);
            if(strokes != null) foreach(var stroke in strokes) { stroke.text = value; stroke.characterSize = label.characterSize; }
            UpdateDetail(side);
            UpdateChargeBar(side);
            if(plate)
            {
                tint ??= new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", effect.IsPositive ? new Color(.76f,.3f,.22f) : new Color(.21f,.43f,.64f));
                plate.SetPropertyBlock(tint);
            }
        }
        void UpdateDetail(int side)
        {
            var detail=side==0?leftDetail:rightDetail;if(!detail)return;
            var effect=side==0?left:right;
            bool selected=selectedSide==side;
            detail.text=(selected?"▼ ":"")+(effect.kind==GateKind.Add?(effect.value<0?"! LOSE ROBOTS":effect.value==0?"SAFE PASS":"ADD ROBOTS"):Category(effect.kind));
            detail.color=selected?new Color(1,.85f,.48f):Color.white;detail.characterSize=.03f;
        }
        void PrepareChargeBars()
        {
            tint ??= new MaterialPropertyBlock();
            for(int side=0;side<2;side++)
            {
                if(chargeFills[side])continue;
                var plate=side==0?leftPlate:rightPlate;if(!plate)continue;
                var frame=side==0?leftFrame:rightFrame;
                var track=Instantiate(plate,frame);track.name="Shot progress track";
                track.transform.localPosition=new Vector3(0,.78f,-.36f);
                track.transform.localRotation=Quaternion.Euler(32,0,0);
                track.transform.localScale=new Vector3(2.8f,.07f,.035f);
                tint.SetColor("_BaseColor",new Color(.06f,.12f,.16f));track.SetPropertyBlock(tint);
                var fill=Instantiate(plate,frame);fill.name="Shot progress fill";
                fill.transform.localRotation=track.transform.localRotation;
                tint.SetColor("_BaseColor",new Color(1,.8f,.35f));fill.SetPropertyBlock(tint);
                chargeTracks[side]=track.transform;chargeFills[side]=fill.transform;
            }
        }
        void UpdateChargeBar(int side)
        {
            if(!chargeFills[side])return;
            var effect=side==0?left:right;
            bool show=effect.CanCharge&&effect.shotsPerIncrement>1;
            chargeTracks[side].gameObject.SetActive(show);chargeFills[side].gameObject.SetActive(show);
            float width=2.8f*(side==0?leftHits:rightHits)/Mathf.Max(1,effect.shotsPerIncrement);
            chargeFills[side].localPosition=new Vector3(-1.4f+width*.5f,.79f,-.39f);
            chargeFills[side].localScale=new Vector3(Mathf.Max(.001f,width),.055f,.035f);
        }
        public bool Tick(float dt, SquadController squad, out GateEffect selected)
        {
            selected = default;
            if (!gameObject.activeSelf) return false;
            if (Resolved)
            {
                exitTime += dt;
                collectedFrame.localScale = Vector3.one * Mathf.Max(0, 1.12f - exitTime * 3.6f);
                transform.position += Vector3.up * (dt * 2);
                if (exitTime >= .3f) gameObject.SetActive(false);
                return false;
            }
            transform.position += Vector3.back * (2.5f * dt);
            leftPulse = Mathf.Max(0, leftPulse - dt * 5); rightPulse = Mathf.Max(0, rightPulse - dt * 5);
            leftLabel.transform.localScale = Vector3.one * (1 + (ReducedMotion ? 0 : leftPulse * .16f));
            rightLabel.transform.localScale = Vector3.one * (1 + (ReducedMotion ? 0 : rightPulse * .16f));
            leftLabel.color=Color.Lerp(Color.white,new Color(1,.86f,.5f),leftPulse);
            rightLabel.color=Color.Lerp(Color.white,new Color(1,.86f,.5f),rightPulse);
            bool inLane=singleLane?Mathf.Abs(squad.transform.position.x-laneX)<=1.15f:Choice(squad.transform.position.x)>=0;
            bool chooseLeft = singleLane || Choice(squad.transform.position.x) == 0;
            int preview=singleLane?(inLane?0:-1):Choice(squad.transform.position.x);
            if(selectedSide!=preview) { selectedSide=preview;UpdateDetail(0);UpdateDetail(1); }
            if (transform.position.z > .6f) return false;
            Resolved = true;
            if(!inLane) { gameObject.SetActive(false);return false; }
            selected = chooseLeft ? left : right;
            squad.Apply(selected);
            collectedFrame = chooseLeft ? leftFrame : rightFrame;
            (chooseLeft ? rightFrame : leftFrame).gameObject.SetActive(false);
            return true;
        }
        static string Category(GateKind kind)
        {
            switch (kind)
            {
                case GateKind.Add: return "RECRUIT";
                case GateKind.Multiply: return "SQUAD SIZE";
                case GateKind.Damage: return "FIREPOWER";
                case GateKind.FireRate: return "RAPID FIRE";
                default: return "HEAVY CANNON";
            }
        }
        public void ResetRow() { Resolved = false; gameObject.SetActive(false); }
    }
}
