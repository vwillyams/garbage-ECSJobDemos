using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
    // IMPORTANT NOTE:
    // UnsafeLinkedListNode may NOT be put into any memory owned by a class.
    // The memory containing it must ALWAYS be allocated with malloc instead, also it can never be on the stack.
    // it takes pointers to other nodes and thus can't handle a moving GC if the data was on a class
    unsafe struct UnsafeLinkedListNode
    {
        public UnsafeLinkedListNode*     prev;
        public UnsafeLinkedListNode*     next;

        unsafe static public void InitializeList(UnsafeLinkedListNode* list)
        {
            list->prev = list;
            list->next = list;
        }


        public bool IsEmpty
        {
            get
            {
                fixed (UnsafeLinkedListNode* list = &this)
                {
                    return list == next;
                }
            }
        }

        unsafe public UnsafeLinkedListNode* Begin()
        {
            return next;
        }

        unsafe public UnsafeLinkedListNode* Back()
        {
            return prev;
        }


        unsafe public UnsafeLinkedListNode* End()
        {
            fixed (UnsafeLinkedListNode* list = &this)
            {
                return list;
            }
        }

        unsafe public void Add(UnsafeLinkedListNode* node)
        {
            fixed (UnsafeLinkedListNode* list = &this)
            {
                InsertBefore(list, node);
            }
        }

        unsafe static public void InsertBefore(UnsafeLinkedListNode* pos, UnsafeLinkedListNode* node)
        {
            Assert.IsTrue(node != pos);
            Assert.IsFalse(node->IsInList);

            node->prev = pos->prev;
            node->next = pos;

            node->prev->next = node;
            node->next->prev = node;
        }

        unsafe static public void InsertListBefore(UnsafeLinkedListNode* pos, UnsafeLinkedListNode* srcList)
        {
            Assert.IsTrue(pos != srcList);
            Assert.IsFalse(srcList->IsEmpty);

            // Insert source before pos
            UnsafeLinkedListNode* a = pos->prev;
            UnsafeLinkedListNode* b = pos;
            a->next = srcList->next;
            b->prev = srcList->prev;
            a->next->prev = a;
            b->prev->next = b;

            // Clear source list
            srcList->next = srcList;
            srcList->prev= srcList;
        }

        unsafe public void Remove()
        {
            if (prev != null)
            {
                prev->next = next;
                next->prev = prev;
                prev = null;
                next = null;
            }
        }

        public bool IsInList
        {
            get { return prev != null; }
        }
    }
}
