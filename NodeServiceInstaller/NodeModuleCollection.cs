using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MadsKristensen.NodeServiceInstaller
{
	public class NodeModuleCollection : KeyedCollection<string,NodeModule>
	{
		public NodeModuleCollection()
		: base(StringComparer.OrdinalIgnoreCase)
		{
		}

		public void AddRange(IEnumerable<NodeModule> copyFrom)
		{
			foreach (NodeModule i in copyFrom)
			{
				Add(i);
			}
		}

		public void AddOrReplaceRange(IEnumerable<NodeModule> copyFrom)
		{
			foreach (NodeModule i in copyFrom)
			{
				AddOrReplace(i);
			}
		}

		public void AddOrReplace(NodeModule item)
		{
			if (Contains(item.Name))
				Remove(item.Name);
			Add(item);
		}

		protected override string GetKeyForItem(NodeModule item)
		{
			return item.Name;
		}

		public NodeModule Find(string name)
		{
			return Contains(name) ? this[name] : null;
		}

		public int RemoveAll(Predicate<NodeModule> match)
		{
			if (match == null)
				throw new ArgumentNullException("match");

			if (Dictionary == null)
				return ((List<NodeModule>) Items).RemoveAll(match);

			int deleted = 0;
			int x = Count;
			while (--x > 0)
			{
				NodeModule t = this[x];
				if (match(t))
				{
					Remove(t);
					deleted++;
				}
			}
			return deleted;
		}

		public void Sort()
		{
			((List<NodeModule>) Items).Sort((a,b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		}

		public void Sort(Comparison<NodeModule> comparison)
		{
			if (comparison == null)
				throw new ArgumentNullException("comparison");

			((List<NodeModule>) Items).Sort(comparison);
		}
	}
}