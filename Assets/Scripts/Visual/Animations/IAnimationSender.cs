using System;

public interface IAnimationSender
{
    public Action<string, bool, float> OnAnimateCommand { get; set; }
}