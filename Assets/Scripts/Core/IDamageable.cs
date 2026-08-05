// Contract for anything that can take damage (player, dummy, etc)
// Контракт для всего, что может получать урон (игрок, манекен и т.д.)
public interface IDamageable
{
    void TakeDamage(float amount); // Apply damage amount // Нанести урон
}