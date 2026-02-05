using UnityEngine;

/// <summary>
/// Simple destructible prop that dies when health is low.
/// </summary>
public class Prop : Unit
{

    protected override void Die()
    {
        base.Die();
        Destroy(gameObject);
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);

        if (!IsDead && currentHealth <= 0.1f)
        {
            Die();
        }
    }
}
