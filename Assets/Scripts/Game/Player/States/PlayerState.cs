public abstract class PlayerState
{
    protected PlayerController Controller;

    public PlayerState(PlayerController controller)
    {
        this.Controller = controller;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void Update();   
    public virtual void FixedUpdate() { }
}