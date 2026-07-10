using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
public class ProgressStep
{
    public Action<object[]> OnStart;
    public Action OnEnd;
    public object[] Data;
}

public class Progresses
{
    private Queue<ProgressStep> _steps;
    private ProgressStep _currentStep;

    public Queue<ProgressStep> Steps => _steps;

    public Progresses()
    {
        _steps = new Queue<ProgressStep>();
        _currentStep = null;
    }

    public void Add(ProgressStep step)
    {
        _steps.Enqueue(step);
    }
    public void Dequeue()
    {
        if (_steps == null) return;
        if (_steps.Count == 0) return;
        _steps.Dequeue();
    }
    public void AddAndTryNext(ProgressStep step)
    {
        Add(step);
        TryNext();
    }

    public void EndAll()
    {
        EndCurrentStep();
        _steps.Clear();
    }

    public void Start()
    {
        TryNext();
    }
    public void EndCurrentAndNext()
    {
        EndCurrentStep();
        TryNext();
    }
    private void EndCurrentStep()
    {
        if (_currentStep == null) return;
        _currentStep.OnEnd?.Invoke();
        if (_steps.Count > 0)
            _steps.Dequeue();
        _currentStep = null;
    }
    private void TryNext()
    {
        if (_currentStep != null) return;
        if (_steps.Count == 0) return;
        _currentStep = _steps.Peek();
        _currentStep.OnStart?.Invoke(_currentStep.Data);
    }
}
}
