using UnityEngine;
using Tobii.XR;
using System.Collections.Generic;

#region Presentation Events
public class PresentationStartEvent : SDD.Events.Event
{

}

public class PresentationFinishEvent : SDD.Events.Event
{

}
#endregion

#region ViewMode Events
public class ViewModeStartEvent : SDD.Events.Event
{

}

public class ViewModeFinishEvent : SDD.Events.Event
{

}
#endregion

#region Paths
public class SetPathEvents : SDD.Events.Event
{
    public string Path;
}
#endregion

#region Slides
public class SetSlidesTextureEvents : SDD.Events.Event
{
    public Texture2D[] textures;
}
#endregion

#region Reset
// Sort of reset for process which does not check the end of the view mode
public class EndViewModeEvent : SDD.Events.Event
{

}
#endregion

#region Score
public class SetGoodScoreEvent : SDD.Events.Event
{

}

public class SetOKScoreEvent : SDD.Events.Event
{

}

public class SetBadScoreEvent : SDD.Events.Event
{

}
#endregion

#region HMD
public class ActivateHMDConfigurationEvent : SDD.Events.Event
{

}

public class DeactivateHMDConfigurationEvent : SDD.Events.Event
{

}
#endregion