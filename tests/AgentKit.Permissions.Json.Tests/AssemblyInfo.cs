// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Several cases observe the process-wide AgentKit activity source and meter. Those listeners cannot be scoped to one
// test, so the assembly runs its classes sequentially and a case never observes another case's measurements.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
