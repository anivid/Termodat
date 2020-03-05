namespace Termodat
{
    public enum TransitionCondition //условие перехода на следующий шаг
    {
        Tcalc, // T расчетная = SP
        ManualAccept, // Ручное подтверждение
        Tmeasure //T измеренная = SP
    }
}
