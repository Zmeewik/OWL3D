using System;

public interface IAnimationSender
{
    public Action<string, string, bool> OnAnimateCommand { get; set; }
}