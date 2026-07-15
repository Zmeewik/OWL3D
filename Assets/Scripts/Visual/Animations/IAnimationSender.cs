using System;

public interface IAnimationSender
{
    public Action<string, string, bool, float> OnAnimateCommand { get; set; }
}