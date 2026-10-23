// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff
{
    internal enum OperatorOperandKind
    {
        SID,
        Boolean,
        Number,
        Array,
        Delta,

        // Compound
        NumberNumber,
        SID_SID_Number,
    }
}
