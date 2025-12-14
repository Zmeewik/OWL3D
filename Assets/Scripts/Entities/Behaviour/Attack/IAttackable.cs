public interface IAttackable
{
    void OnAttackPressed(int attackIndex);
    void OnAttackReleased(int attackIndex);
    void OnAttackHeld(int attackIndex);
    void OnBlockPressed();
    void OnBlockReleased();
    void OnLegHit();
}