using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
    unsafe struct LinkedListNode
    {
        public LinkedListNode*     prev;
        public LinkedListNode*     next;

        unsafe static public void InitializeList(LinkedListNode* list)
        {
            list->prev = list;
            list->next = list;
        }

        public bool IsEmpty
        {
            get { return prev == next; }
        }


        unsafe public LinkedListNode* begin()
        {
            return next;
        }

        unsafe public LinkedListNode* end()
        {
            fixed (LinkedListNode* list = &this)
            {
                return list;
            }
        }

        unsafe public void push_back(LinkedListNode* node)
        {
            fixed (LinkedListNode* list = &this)
            {
                InsertBefore(list, node);
            }
        }

        unsafe static public void InsertBefore(LinkedListNode* pos, LinkedListNode* node)
        {
            Assert.IsTrue(node != pos);
            Assert.IsFalse(node->IsInList());

            node->prev = pos->prev;
            node->next = pos;

            node->prev->next= node;
            node->next->prev = node;
        }

        unsafe public void Remove()
        {
            Assert.IsTrue(IsInList());

            prev->next = next;
            next->prev = prev;
            prev = null;
            next = null;
        }

        unsafe bool IsInList()
        {
            return prev != null;
        }
    }
}
