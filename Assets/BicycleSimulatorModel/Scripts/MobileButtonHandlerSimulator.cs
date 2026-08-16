using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MobileButtonHandlerSimulator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
 
public int buttonPressed;
public int direction;
 
public void OnPointerDown(PointerEventData eventData){
    buttonPressed = 1 * direction;
}
 
public void OnPointerUp(PointerEventData eventData){
    buttonPressed = 0;
}
}
