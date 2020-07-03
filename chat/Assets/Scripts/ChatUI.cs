using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FairyGUI;
using static FairyGUI.UIContentScaler;

public class ChatUI : MonoBehaviour
{
    GComponent _mainView;
    GList _list;
    GTextInput _textInput;

    List<string> contentList = new List<string>();

    // Start is called before the first frame update
    void Start()
    {
        Stage.inst.onKeyDown.Add(OnKeyDown);
        _mainView = this.GetComponent<UIPanel>().ui;
        _mainView.GetChild("send_btn").onClick.Add(OnClickSend);
        
        _list = _mainView.GetChild("list").asList;
        _list.itemRenderer = RenderListItem;
        _list.numItems = contentList.Count;

        _textInput = _mainView.GetChild("input").asTextInput;
    }

    void OnClickSend()
    {
        contentList.Add(_textInput.text);
        _list.numItems = contentList.Count;
        _list.scrollPane.ScrollBottom();
    }

    void RenderListItem(int index, GObject obj)
    {
        GComponent component = obj.asCom;
        component.GetChild("content").asTextField.text = contentList[index];
    }

    void OnKeyDown(EventContext context)
    {
        if (context.inputEvent.keyCode == KeyCode.Escape)
        {
            Application.Quit();
        }
    }
}
