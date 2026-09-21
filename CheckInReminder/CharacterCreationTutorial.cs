namespace CheckInReminder;

internal static class CharacterCreationTutorial
{
    public const string ThreeViewPrompt = "生成角色的绿底三视图";
    public const string VideoPrompt = """
上图角色在纯绿背景中进行以下动作：“他先从画外向镜头内伸出一只手，然后扒着边框入镜了，只露出脑袋就行，然后打了个招呼，最后退出了镜头，从哪个边框出来的就从哪个方向出去”
""";
    public const string IdlePrompt = "参考图一，生成图二角色的图片，让图二的角色保持图一角色的动作，键盘鼠标保持图一的样式  ";
    public const string PressPrompt = """
让上图角色举起来的手去按一下键盘，生成三张图，分别是它按到键盘左侧、按到键盘中间、按到键盘右侧的效果，全部都是透明 PNG  ，保持当前比例
""";
}
