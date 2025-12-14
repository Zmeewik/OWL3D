using System;

public interface IWeaponCommand
{
    public Action<string> OnWeaponCommand {get; set;}
}