//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI upgrade button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Upgrade Button")]
public class RCCP_UI_Upgrade : MonoBehaviour {

    public UpgradeClass upgradeClass = UpgradeClass.Speed;
    public enum UpgradeClass { Speed, Handling, Brake }

    public void OnClick() {

        RCCP_CustomizationManager handler = RCCP_CustomizationManager.Instance;
        RCCP_CustomizationApplier applier = handler.vehicle;

        switch (upgradeClass) {

            case UpgradeClass.Speed:
                if (applier.UpgradeManager.engineLevel < 5) {
                    handler.UpgradeSpeed();
                }
                break;
            case UpgradeClass.Handling:
                if (applier.UpgradeManager.handlingLevel < 5) {
                    handler.UpgradeHandling();
                }
                break;
            case UpgradeClass.Brake:
                if (applier.UpgradeManager.brakeLevel < 5) {
                    handler.UpgradeBrake();
                }
                break;

        }

    }

}
