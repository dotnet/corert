//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

// TODO: Implement Unix write barriers

.global RhpBulkWriteBarrier
RhpBulkWriteBarrier:
ret

.global RhpAssignRef
.global RhpCheckedAssignRef
RhpAssignRef:
RhpCheckedAssignRef:
mov %rsi, (%rdi)
ret
