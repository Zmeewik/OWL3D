using System;

public interface IWeaponCommand
{
    public Action<WeaponCommand> OnWeaponCommand {get; set;}
}
