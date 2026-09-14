namespace Gw2.Native;

internal enum AgentType : int
{
    Error = -1,
    Character = 0,
    Gadget = 10,
    GadgetAttackTarget = 11,
    Item = 15
}

internal enum AgentCategory : int
{
    Character = 0,
    Dynamic = 1,
    Keyframed = 2
}
