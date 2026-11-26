namespace LearnEngine.Core;

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or the failure.</summary>
public union Outcome<T0, T1>(T0, T1);

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or one of 2 failures.</summary>
public union Outcome<T0, T1, T2>(T0, T1, T2);

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or one of 3 failures.</summary>
public union Outcome<T0, T1, T2, T3>(T0, T1, T2, T3);

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or one of 4 failures.</summary>
public union Outcome<T0, T1, T2, T3, T4>(T0, T1, T2, T3, T4);

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or one of 5 failures.</summary>
public union Outcome<T0, T1, T2, T3, T4, T5>(T0, T1, T2, T3, T4, T5);

/// <summary>What an operation gave back: the success, <typeparamref name="T0"/>, or one of 6 failures.</summary>
public union Outcome<T0, T1, T2, T3, T4, T5, T6>(T0, T1, T2, T3, T4, T5, T6);
