namespace CheckInReminder;

public enum PetKeyboardPose
{
    Left,
    Center,
    Right,
}

public static class PetKeyboardPoseMapper
{
    public static PetKeyboardPose Map(float normalizedX, bool hasCenterPose)
    {
        var x = Math.Clamp(normalizedX, 0f, 1f);
        if (!hasCenterPose)
        {
            return x < 0.5f ? PetKeyboardPose.Left : PetKeyboardPose.Right;
        }

        return x switch
        {
            < 1f / 3f => PetKeyboardPose.Left,
            < 2f / 3f => PetKeyboardPose.Center,
            _ => PetKeyboardPose.Right,
        };
    }
}
