using RealSteel.Dialogue;

// LHM UI 스택에 열린 창으로 등록한다. 일시정지 메뉴가 Esc를 양보하고 게임 시계도 멈춘다. 이 파일 밖에서는 UiStack을 직접 참조하지 않는다
public static class UiStackGate
{
    public static void Open(object owner) => UiStack.Push(owner);

    public static void Close(object owner) => UiStack.Remove(owner);
}
