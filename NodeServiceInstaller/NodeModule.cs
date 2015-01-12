using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pri.LongPath;

namespace MadsKristensen.NodeServiceInstaller
{
	public sealed class NodeModule : IEnumerable<NodeModule>
	{
		public const string ModulesDirectoryName = "node_modules";
		public const string BinDirectoryName = "bin";

		readonly NodeModuleCollection _children = new NodeModuleCollection();
		readonly List<VersionDependency> _dependencies = new List<VersionDependency>();
		Dictionary<string,string> _scripts = new Dictionary<string, string>();

		int WeightAsParent { get; set; }
		int FullWeightAsParent { get; set; }

		public Dictionary<string,string> Scripts
		{
			get
			{
				return _scripts;
			}
		}

		public string Name { get; private set; }
		public string RealPath { get; private set; }
		public string OriginalPath { get; private set; }
		public NodeModule Parent { get; private set; }
		public string Version { get; private set; }
		// Weight = the length of the longest directory in this module
		public int Weight { get; private set; }
		public int FullWeight { get; private set; }
		// WeightAsParent = the length of this module as it pertains to sub-modules (only counts the module folder itself and the "node_modules" sub-dir, but none of the others)
		public bool Add { get; set; }
		public bool Remove { get; set; }

		[JsonProperty("dependencies")]
		public NodeModuleCollection Children
		{
			get
			{
				return _children;
			}
		}

		public List<VersionDependency> Dependencies
		{
			get
			{
				return _dependencies;
			}
		}

		public NodeModule(string name, NodeModule parent)
		{
			Name = name;
			Parent = parent;
		}

		public void DeserializeJson(JObject data)
		{
			Version = (string) data["version"];
			RealPath = (string) data["realPath"];
			OriginalPath = RealPath;

			JObject v = (JObject) data["scripts"];
			if (v != null)
			{
				foreach (KeyValuePair<string, JToken> i in v)
				{
					Scripts.Add(i.Key, (string) i.Value);
				}
			}

			v = (JObject) data["_dependencies"];
			if (v != null)
			{
				foreach (KeyValuePair<string, JToken> i in v)
				{
					string depName = i.Key;
					string depValue = (string) i.Value;
					Dependencies.Add(new VersionDependency(depName, depValue));
				}
			}

			v = (JObject) data["dependencies"];
			if (v != null)
			{
				foreach (KeyValuePair<string, JToken> i in v)
				{
					var childData = (JObject) i.Value;
					if (childData["name"] != null)
					{
						NewChild(i.Key);
					}
				}
				foreach (KeyValuePair<string, JToken> i in v)
				{
					var childData = (JObject) i.Value;
					if (childData["name"] != null)
					{
						NodeModule childObj = Children.Find(i.Key);
						if (childObj != null)
						{
							childObj.DeserializeJson(childData);
						}
					}
					else
					{
						NodeModule dependencyObj = null;
						NodeModule scope = Parent;
						while (scope != null && dependencyObj == null)
						{
							dependencyObj = scope.Children.Find(i.Key);
							if (dependencyObj == null)
								scope = scope.Parent;
						}
						if (dependencyObj == null)
						{
							Console.WriteLine("*** Can't find dependency '" + i.Key + "' for " + ToString());
							throw new InvalidOperationException("Can't find dependency.");
						}
					}
				}
			}
		}

		public void CalculateWeights(string rootPath, IList<string> ignorePaths)
		{
			List<Regex> ignorePatterns = new List<Regex>(ignorePaths.Count + 1)
			{
				FilePatternToRegex.Convert(ModulesDirectoryName)
			};
			ignorePatterns.AddRange(ignorePaths.Select(FilePatternToRegex.Convert));
			// TODO: check patterns, verify they are correct

			CalculateWeights(rootPath, ignorePatterns);
		}

		void CalculateWeights(string rootPath, IList<Regex> ignorePatterns)
		{
			// find longest path in this module (not including sub-modules)

			// Weight = longest directory in this module (not including sub-modules), and not including the portion of the path before this module
			// FullWeight = the parent's WeightAsParent plus this Weight of this module

			int parentWeight = Parent != null ? Parent.FullWeightAsParent : 0;
			Weight = Name.Length + 1 + FindLongestFileLength(rootPath, ignorePatterns);
			WeightAsParent = Name.Length + ModulesDirectoryName.Length + 2;
			FullWeightAsParent = parentWeight + WeightAsParent;
			FullWeight = parentWeight + Weight;

			if (Children.Count > 0)
			{
				// {Parent.WeightAsParent} / {name} / node_modules /
				foreach (NodeModule i in Children)
				{
					i.CalculateWeights(Path.Combine(rootPath, ModulesDirectoryName, i.Name), ignorePatterns);
				}
			}
		}

		int FindLongestFileLength(string dir, IList<Regex> ignorePatterns)
		{
			int baseLength = dir.Length;
			int maxLength = 0;

			var stack = new Stack<string>();
			stack.Push(dir);
			while (stack.Count > 0)
			{
				dir = stack.Pop();

				foreach (string s in Directory.GetDirectories(dir).Where(f => !ignorePatterns.Any(i => i.IsMatch(Path.GetFileName(f)))))
				{
					int x = s.Length - baseLength;
					if (x > maxLength)
						maxLength = x;

					stack.Push(s);
				}

				foreach (string s in Directory.GetFiles(dir).Where(f => !ignorePatterns.Any(i => i.IsMatch(Path.GetFileName(f)))))
				{
					int x = s.Length - baseLength;
					if (x > maxLength)
						maxLength = x;
				}
			}

			return maxLength;
		}

		public void SortChildren()
		{
			Children.Sort();
			foreach (NodeModule i in Children)
			{
				i.SortChildren();
			}
		}

		public IEnumerator<NodeModule> GetEnumerator()
		{
			return _children.Where(x => !x.Remove).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString()
		{
			return ToString(true);
		}

		string ToString(bool includeVersion)
		{
			if (includeVersion)
			{
				if (Parent != null)
					return Parent.ToString(false) + " > " + Name + "@" + Version + " (" + WeightAsParent.ToString() + "/" + Weight.ToString() + "/" + FullWeight.ToString() + ")";
				return Name + "@" + Version + " (" + WeightAsParent.ToString() + "/" + Weight.ToString() + "/" + FullWeight.ToString() + ")";
			}

			if (Parent != null)
				return Parent.ToString(false) + " > " + Name + " (" + WeightAsParent.ToString() + "/" + Weight.ToString() + "/" + FullWeight.ToString() + ")";
			return Name + " (" + WeightAsParent.ToString() + "/" + Weight.ToString() + "/" + FullWeight.ToString() + ")";
		}

		public NodeModule FirstNonDeletedParent()
		{
			NodeModule current = this;
			while (current != null && current.Remove)
			{
				current = current.Parent;
			}
			return current;
		}

		IEnumerable<NodeModule> Family()
		{
			// get all modules that can "see" this module
			// this would be, recursively starting with the parent's children (not including the parent itself), any that are the same name as this one

			return this.WalkDepthFirst().TakeWhile(x => !EqualsName(x, this));
		}

		public IEnumerable<NodeModule> Scope()
		{
			// get all modules that tihs module can "see"

			var alreadyFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			NodeModule current = this;
			while (current != null)
			{
				foreach (var i in current.Children.Where(x => !x.Remove))
				{
					if (alreadyFound.Add(i.Name))
						yield return i;
				}
				current = current.Parent;
			}
		}

		public IEnumerable<NodeModule> FindDependencies()
		{
			return Scope().Where(x => Dependencies.Any(d => EqualsName(x, d.Name)));
		}

		public IEnumerable<NodeModule> FindDependants() 
		{
			return Family().Where(x => x.Dependencies.Any(d => EqualsName(x, d.Name)));
		}

		public bool IsElligibleToMove(NodeModule potentialParent)
		{
			// is elligible if all of the nodes dependencies either:
			//    (a) exist at the new scope
			//    or (b) exist as a child of obj (even if removed--we'll unremove it
			foreach (VersionDependency i in Dependencies)
			{
				NodeModule existingDep = Scope().FirstOrDefault(x => EqualsName(x, i.Name));
				if (existingDep != null)
				{
					NodeModule newDep = potentialParent.Scope().FirstOrDefault(x => EqualsVersion(x, existingDep));
					if (newDep == null)
					{
						newDep = Children.Find(i.Name);
						if (newDep == null)
							return false;
					}
				}
			}
			return true;
		}

		NodeModule NewChild(string name)
		{
			var childObj = new NodeModule(name, this);
			Children.Add(childObj);

			return childObj;
		}

		public NodeModule NewChildFromCopy(NodeModule source)
		{
			var childObj = new NodeModule(source.Name, this)
			{
				RealPath = Path.Combine(RealPath, ModulesDirectoryName, source.Name),
				OriginalPath = source.OriginalPath,
				Version = source.Version,
				Weight = source.Weight,
				WeightAsParent = source.WeightAsParent,
				FullWeightAsParent = FullWeightAsParent + source.WeightAsParent,
				FullWeight = WeightAsParent + source.Weight,
				Add = source.Add,
				Remove = source.Remove
			};
			childObj.Dependencies.AddRange(source.Dependencies);
			Children.Add(childObj);
			foreach (NodeModule i in source.Children)
			{
				childObj.NewChildFromCopy(i);
			}
			return childObj;
		}

		public void MarkRemoved()
		{
			if (Children.Count > 0)
			{
				foreach (NodeModule i in Children.ToArray())
				{
					i.MarkRemoved();
				}
			}

			Remove = true;
		}

		public static bool EqualsName(NodeModule a, NodeModule b)
		{
			return string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
		}

		public static bool EqualsName(NodeModule a, string b)
		{
			return string.Equals(a.Name, b, StringComparison.OrdinalIgnoreCase);
		}

		public static bool EqualsName(string a, string b)
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		public static bool EqualsVersion(NodeModule a, NodeModule b)
		{
			return EqualsName(a, b) && string.Equals(a.Version, b.Version, StringComparison.OrdinalIgnoreCase);
		}

		public static bool EqualsVersion(NodeModule a, string b)
		{
			return string.Equals(a.Version, b, StringComparison.OrdinalIgnoreCase);
		}

		public static bool EqualsVersion(string a, string b)
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		public IEnumerable<NodeModule> Ancestors()
		{
			NodeModule current = this;
			while (current != null)
			{
				yield return current;
				current = current.Parent;
			}
		}

		public IEnumerable<NodeModule> WalkDepthFirst()
		{
			var stack = new Stack<NodeModule>();
			stack.Push(this);
			while (stack.Count != 0)
			{
				NodeModule current = stack.Pop();
				foreach (NodeModule i in current)
				{
					stack.Push(i);
				}
				yield return current;
			}
		}

		public IEnumerable<NodeModule> WalkBreadthFirst()
		{
			var queue = new Queue<NodeModule>();
			queue.Enqueue(this);
			while (queue.Count != 0)
			{
				NodeModule current = queue.Dequeue();
				foreach (NodeModule i in current)
				{
					queue.Enqueue(i);
				}
				yield return current;
			}
		}
	}
}