using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageArtStylePopup : MonoBehaviour
{
    public int paintingIndex;
    public Texture2D[] paintings;
    public GameObject paintingPopup;
    public RawImage popup;

    public void SetPaintingPopupState(bool state) {  paintingPopup.SetActive(state); }

    public void FireOffPopUp() {
        StartCoroutine(SetPaintingActive());
    }

    public IEnumerator SetPaintingActive() {
        SetPaintingPopupState(true);
        yield return new WaitForSeconds(1.5f);
        SetPaintingPopupState(false);
    }

    public void Sequence() {
        paintingIndex++;
        paintingIndex %= paintings.Length;
        popup.texture = paintings[paintingIndex];
    }
}
