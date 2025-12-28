using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;

namespace ApiDiff
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ApiDiff export <assembly-path> [output.json]\n       ApiDiff compare <left.api.json> <right.api.json>");
                return 1;
            }

            var cmd = args[0];
            if (cmd == "export")
            {
                return RunExport(args);
            }
            else if (cmd == "compare")
            {
                return RunCompare(args);
            }
            else if (cmd == "compare-related")
            {
                return RunCompareRelated(args);
            }
            else if (cmd == "member-diff-related")
            {
                return RunMemberDiffRelated(args);
            }
            else
            {
                Console.WriteLine("Unknown command. Supported: export, compare");
                return 1;
            }
        }

        private static int RunCompare(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff compare <left.api.json> <right.api.json>");
                return 1;
            }

            var leftPath = args[1];
            var rightPath = args[2];
            if (!File.Exists(leftPath) || !File.Exists(rightPath))
            {
                Console.WriteLine("One of the manifest files does not exist.");
                return 1;
            }

            var left = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(leftPath)) ?? new ApiManifest();
            var right = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(rightPath)) ?? new ApiManifest();

            // Build dictionaries by full type name (namespace + name)
            string FullType(ApiType t) => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
            var leftMap = left.Types.ToDictionary(FullType, t => t);
            var rightMap = right.Types.ToDictionary(FullType, t => t);

            var leftOnly = leftMap.Keys.Except(rightMap.Keys).OrderBy(k=>k).ToList();
            var rightOnly = rightMap.Keys.Except(leftMap.Keys).OrderBy(k=>k).ToList();
            var both = leftMap.Keys.Intersect(rightMap.Keys).OrderBy(k=>k).ToList();

            Console.WriteLine($"Left types: {leftMap.Count}, Right types: {rightMap.Count}");
            Console.WriteLine($"Types only in left: {leftOnly.Count}");
            Console.WriteLine($"Types only in right: {rightOnly.Count}");
            Console.WriteLine($"Types in both: {both.Count}");

            if (leftOnly.Any())
            {
                Console.WriteLine("\nSample types only in left:");
                foreach (var t in leftOnly.Take(20)) Console.WriteLine("  " + t);
            }

            if (rightOnly.Any())
            {
                Console.WriteLine("\nSample types only in right:");
                foreach (var t in rightOnly.Take(20)) Console.WriteLine("  " + t);
            }

            // For types in both, compare members
            var typeChanges = new List<string>();
            foreach (var tn in both)
            {
                var l = leftMap[tn];
                var r = rightMap[tn];
                var lset = new HashSet<string>(l.Members);
                var rset = new HashSet<string>(r.Members);
                var added = rset.Except(lset).ToList();
                var removed = lset.Except(rset).ToList();
                if (added.Count > 0 || removed.Count > 0)
                {
                    typeChanges.Add(tn + $" (+{added.Count}/-{removed.Count})");
                }
            }

            Console.WriteLine($"\nTypes with member differences: {typeChanges.Count}");
            foreach (var t in typeChanges.Take(50)) Console.WriteLine("  " + t);

            // Optionally write a JSON details file (not implemented) — just exit
            return 0;
        }

        private static int RunCompareRelated(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff compare-related <left.api.json> <right.api.json> [keywords]");
                Console.WriteLine("keywords: comma-separated short type names to look for (default includes DataGrid, DataGridRow, DataGridColumn, DataGridCell, DataGridRowHeader, DataGridTemplateColumn, DataGridTextColumn, DataGridCheckBoxColumn, DataGridBoundColumn, DataGridRowGroupHeader)");
                return 1;
            }

            var leftPath = args[1];
            var rightPath = args[2];
            var keywords = args.Length > 3 ? args[3].Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList()
                                     : new List<string>{"DataGrid","DataGridRow","DataGridColumn","DataGridCell","DataGridRowHeader","DataGridTemplateColumn","DataGridTextColumn","DataGridCheckBoxColumn","DataGridBoundColumn","DataGridRowGroupHeader"};

            // optional mapping argument can be provided as 4th arg or via a file path; format: leftNs=>rightNs;... or one mapping per line in file
            var mappings = new List<(string leftPrefix, string rightPrefix)>();
            if (args.Length > 4)
            {
                var mapArg = args[4];
                mappings = ParseMappingsArgOrFile(mapArg);
            }

            if (!File.Exists(leftPath) || !File.Exists(rightPath))
            {
                Console.WriteLine("One of the manifest files does not exist.");
                return 1;
            }

            var left = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(leftPath)) ?? new ApiManifest();
            var right = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(rightPath)) ?? new ApiManifest();

            // map by short name -> list of full types
            string ShortName(ApiType t) => t.Name;
            var leftByShort = left.Types.GroupBy(ShortName).ToDictionary(g=>g.Key, g=>g.ToList());
            var rightByShort = right.Types.GroupBy(ShortName).ToDictionary(g=>g.Key, g=>g.ToList());

            // also build a lookup for right types by full name for mapping checks
            var rightByFull = right.Types.ToDictionary(t => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name, t => t);

            var report = new List<string>();
            foreach (var kw in keywords)
            {
                var leftHas = leftByShort.ContainsKey(kw);
                var rightHas = rightByShort.ContainsKey(kw);
                if (leftHas && rightHas)
                {
                    report.Add($"MATCH: {kw} -> left: {leftByShort[kw].Count} type(s), right: {rightByShort[kw].Count} type(s)");
                }
                else if (leftHas && !rightHas)
                {
                    // attempt namespace-mapped lookup: for each left type with this short name, map its namespace and check right full names
                    var leftTypes = leftByShort[kw];
                    var mappedFound = 0;
                    foreach (var lt in leftTypes)
                    {
                        var mappedFull = MapFullName(lt.Namespace, lt.Name, mappings);
                        if (rightByFull.ContainsKey(mappedFull)) mappedFound++;
                    }
                    if (mappedFound > 0)
                        report.Add($"MATCH VIA NAMESPACE MAP: {kw} -> mapped matches: {mappedFound} of {leftByShort[kw].Count}");
                    else
                        report.Add($"MISSING IN RIGHT: {kw} -> left: {leftByShort[kw].Count} type(s), right: 0");
                }
                else if (!leftHas && rightHas)
                {
                    report.Add($"MISSING IN LEFT: {kw} -> left: 0, right: {rightByShort[kw].Count} type(s)");
                }
                else
                {
                    report.Add($"MISSING BOTH: {kw}");
                }
            }

            Console.WriteLine("DataGrid-related matching report:");
            foreach (var r in report) Console.WriteLine("  " + r);

            // For any left types whose short name includes 'DataGrid' or the keywords, list those not found in right
            var leftRelated = left.Types.Where(t => keywords.Any(kw => t.Name.Contains(kw))).ToList();
            Console.WriteLine($"\nLeft related types: {leftRelated.Count}");
            foreach (var t in leftRelated.OrderBy(t=>t.Name))
            {
                // present if same short name exists OR namespace-mapped full name exists
                var presentInRight = right.Types.Any(rt => rt.Name == t.Name);
                if (!presentInRight && mappings.Any())
                {
                    var mappedFull = MapFullName(t.Namespace, t.Name, mappings);
                    presentInRight = rightByFull.ContainsKey(mappedFull);
                }
                Console.WriteLine($"  {t.Namespace}.{t.Name} -> {(presentInRight?"FOUND":"MISSING")} ({t.Members.Count} members)");
            }

            return 0;
        }

    private static int RunMemberDiffRelated(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff member-diff-related <left.api.json> <right.api.json> [keywords] [ns-mappings] [type-aliases]");
                Console.WriteLine("type-aliases: semicolon-separated leftTypeFull=>rightTypeFull entries to unify type names (e.g. Avalonia.Collections.DataGridCurrentChangingEventArgs=>System.ComponentModel.CancelEventArgs)");
                return 1;
            }

            var leftPath = args[1];
            var rightPath = args[2];
            var keywords = args.Length > 3 && !string.IsNullOrEmpty(args[3]) ? args[3].Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList()
                                     : new List<string>{"DataGrid","DataGridRow","DataGridColumn","DataGridCell","DataGridRowHeader","DataGridTemplateColumn","DataGridTextColumn","DataGridCheckBoxColumn","DataGridBoundColumn","DataGridRowGroupHeader"};
            var mappings = new List<(string leftPrefix, string rightPrefix)>();
            var typeAliases = new Dictionary<string,string>();
            if (args.Length > 4 && !string.IsNullOrEmpty(args[4]))
            {
                // if the arg is a json config file, parse both namespace mappings and type aliases from it
                if (File.Exists(args[4]) && args[4].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    mappings = ParseMappingsArgOrFile(args[4]);
                    typeAliases = ParseTypeAliasesArgOrFile(args[4]);
                }
                else
                {
                    mappings = ParseMappingsArgOrFile(args[4]);
                }
            }
            var fullOutput = args.Any(a => a == "--full");
            var shortTypes = args.Any(a => a == "--short-types");
            var suppressAdded = args.Any(a => a == "--suppress-added" || a == "--no-added");
            int printLimit = 20;
            var limitArg = args.FirstOrDefault(a => a.StartsWith("--limit="));
            if (limitArg != null)
            {
                var parts = limitArg.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[1], out var v)) printLimit = v;
            }
            if (args.Length > 5 && !string.IsNullOrEmpty(args[5]))
            {
                var extra = ParseTypeAliasesArgOrFile(args[5]);
                foreach (var kv in extra) typeAliases[kv.Key] = kv.Value;
            }

            if (!File.Exists(leftPath) || !File.Exists(rightPath))
            {
                Console.WriteLine("One of the manifest files does not exist.");
                return 1;
            }

            var left = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(leftPath)) ?? new ApiManifest();
            var right = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(rightPath)) ?? new ApiManifest();

            // helpers
            string FullName(ApiType t) => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
            var rightFullMap = right.Types.ToDictionary(t => FullName(t), t => t);
            var rightByShort = right.Types.GroupBy(t=>t.Name).ToDictionary(g=>g.Key, g=>g.First());

            var relatedLeft = left.Types.Where(t => keywords.Any(kw => t.Name.Contains(kw))).ToList();

            Console.WriteLine($"Member diffs for {relatedLeft.Count} related left types:\n");

            foreach (var lt in relatedLeft.OrderBy(t=>t.Name))
            {
                // find matching right type: by short name, or namespace-mapped full name
                ApiType rt = null;
                if (rightByShort.TryGetValue(lt.Name, out var candidate)) rt = candidate;
                if (rt == null && mappings.Any())
                {
                    var mappedFull = MapFullName(lt.Namespace, lt.Name, mappings);
                    if (rightFullMap.TryGetValue(mappedFull, out var m)) rt = m;
                }

                // apply type alias normalization for member signatures
                Func<string,string> normalizeTypeNames = s =>
                {
                    if (string.IsNullOrEmpty(s)) return s;
                    foreach (var kv in typeAliases)
                    {
                        s = s.Replace(kv.Key, kv.Value);
                    }
                    if (shortTypes)
                    {
                        // naive strip namespaces: replace 'Namespace.TypeName' with 'TypeName' for known patterns
                        // match sequences of word chars and dots followed by a capitalized identifier
                        // we'll do a simple token replace: split on non-word chars and replace tokens that contain '.'
                        // easier: look for tokens like 'System.Windows.Controls.DataGridLengthUnitType' and take last segment after '.'
                        var parts = s.Split(new[] { ' ', ',', '(', ')', '<', '>', '&' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts.Where(p => p.Contains('.')).Distinct())
                        {
                            var shortName = part.Split('.').Last();
                            s = s.Replace(part, shortName);
                        }
                    }
                    return s;
                };

                // normalize signatures: apply type aliases/short-types and strip parameter names so overloads
                // that only differ in parameter names are considered equal
                string stripParamNames(string sig)
                {
                    // find the parentheses section and remove parameter names inside
                    var i = sig.IndexOf('(');
                    if (i < 0) return sig;
                    var pre = sig.Substring(0, i+1);
                    var rest = sig.Substring(i+1);
                    var j = rest.LastIndexOf(')');
                    if (j < 0) return sig;
                    var paramList = rest.Substring(0, j);
                    var post = rest.Substring(j);
                    if (string.IsNullOrWhiteSpace(paramList)) return pre + post;
                    // split parameters by commas and, for each param, keep only the type token(s)
                    var parts = paramList.Split(',');
                    var newParts = parts.Select(p => {
                        var tokens = p.Trim().Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                        // tokens usually like: "System.Double value" or "ref System.String name" or "System.Collections.Generic.List<System.String> items"
                        // keep tokens until we believe we've consumed the type. We'll keep all tokens except the last if last is a simple identifier.
                        if (tokens.Length == 0) return "";
                        if (tokens.Length == 1) return tokens[0];
                        var last = tokens.Last();
                        // heuristics: if last token starts with a lowercase letter or is an identifier (no punctuation), treat as param name
                        if (char.IsLower(last[0]) || (!last.Contains(".") && last.All(c => char.IsLetterOrDigit(c) || c=='_')))
                        {
                            return string.Join(' ', tokens.Take(tokens.Length-1));
                        }
                        return string.Join(' ', tokens);
                    });
                    var newParamList = string.Join(", ", newParts.Where(x=>!string.IsNullOrEmpty(x)));
                    return pre + newParamList + post;
                }

                // Normalize property-like signatures and dependency/styled-property fields
                // so that an instance property and a static dependency field count as equal
                // when the underlying name and type match. We canonicalize both forms to
                // "<Type> <Name> { property }".
                string NormalizePropertyLike(string sig)
                {
                    if (string.IsNullOrEmpty(sig)) return sig;

                    // If already a property signature like: "System.String Name { get; set; }"
                    // replace the accessor block with a canonical token.
                    var braceOpen = sig.IndexOf('{');
                    var braceClose = sig.LastIndexOf('}');
                    if (braceOpen >= 0 && braceClose > braceOpen)
                    {
                        var before = sig.Substring(0, braceOpen).Trim();
                        // take the type+name part
                        return before + " { property }";
                    }

                    // Otherwise it may be a field signature like: "Avalonia.StyledProperty`1<System.String> NameProperty"
                    // or "Avalonia.AvaloniaProperty`1<System.String> NameProperty". Try to detect the pattern
                    // and convert to the canonical property form.
                    var parts = sig.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var typePart = parts[0];
                        var namePart = parts[1];

                        // common field suffix for dependency/styled properties
                        var suffixes = new[] { "Property", "StyledProperty", "AvaloniaProperty" };
                        foreach (var suf in suffixes)
                        {
                            if (namePart.EndsWith(suf, StringComparison.Ordinal))
                            {
                                var baseName = namePart.Substring(0, namePart.Length - suf.Length);
                                // attempt to extract the generic argument type if present: "...`1<System.String>"
                                var innerType = typePart;
                                var lt = typePart.IndexOf('<');
                                var rt = typePart.LastIndexOf('>');
                                if (lt >= 0 && rt > lt)
                                {
                                    innerType = typePart.Substring(lt + 1, rt - lt - 1).Trim();
                                }
                                // fallback: if innerType still contains a '`' marker, try to remove the backtick part
                                var bt = innerType.IndexOf('`');
                                if (bt >= 0)
                                {
                                    // e.g. "Namespace.Type`1" -> take up to backtick
                                    innerType = innerType.Substring(0, bt);
                                }
                                return innerType + " " + baseName + " { property }";
                            }
                        }
                    }

                    return sig;
                }

                var leftMembers = new HashSet<string>(lt.Members.Select(m=>NormalizePropertyLike(stripParamNames(normalizeTypeNames(m)))));
                var rightMembers = rt != null ? new HashSet<string>(rt.Members.Select(m=>NormalizePropertyLike(stripParamNames(normalizeTypeNames(m))))) : new HashSet<string>();

                var added = rightMembers.Except(leftMembers).OrderBy(x=>x).ToList();
                var removed = leftMembers.Except(rightMembers).OrderBy(x=>x).ToList();

                Console.WriteLine($"{FullName(lt)} -> match: {(rt!=null?FullName(rt):"<none>")} ; members: left={leftMembers.Count} right={(rt!=null?rightMembers.Count:0)} added={added.Count} removed={removed.Count}");
                if (!suppressAdded && added.Count>0)
                {
                    Console.WriteLine("  + Added members (in right, not in left):");
                    var doAll = fullOutput || printLimit <= 0;
                    var toPrint = doAll ? added : added.Take(printLimit);
                    foreach (var a in toPrint) Console.WriteLine("    " + a);
                    if (!doAll && added.Count>printLimit) Console.WriteLine($"    ... {added.Count-printLimit} more");
                }
                if (removed.Count>0)
                {
                    Console.WriteLine("  - Removed members (in left, not in right):");
                    var doAll = fullOutput || printLimit <= 0;
                    var toPrint = doAll ? removed : removed.Take(printLimit);
                    foreach (var rmm in toPrint) Console.WriteLine("    " + rmm);
                    if (!doAll && removed.Count>printLimit) Console.WriteLine($"    ... {removed.Count-printLimit} more");
                }
                Console.WriteLine();
            }

            return 0;
        }

        private static int RunExport(string[] args)
        {
            var assemblyPath = args.Length > 1 ? args[1] : null;
            if (string.IsNullOrEmpty(assemblyPath))
            {
                Console.WriteLine("No assembly path provided.");
                return 1;
            }

            var outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(assemblyPath, ".api.json");

            var asm = AssemblyDefinition.ReadAssembly(assemblyPath);

            var manifest = new ApiManifest();

            foreach (var module in asm.Modules)
            {
                foreach (var type in module.Types)
                {
                    if (!IsPublicType(type))
                        continue;

                    var t = new ApiType
                    {
                        Namespace = type.Namespace,
                        Name = type.Name,
                        Kind = GetTypeKind(type),
                        Members = new List<string>()
                    };

                    foreach (var method in type.Methods)
                    {
                        if (!IsPublicMethod(method))
                            continue;
                        t.Members.Add(MethodSignature(method));
                    }

                    foreach (var field in type.Fields)
                    {
                        if (!IsPublicField(field))
                            continue;
                        t.Members.Add(FieldSignature(field));
                    }

                    foreach (var prop in type.Properties)
                    {
                        if (!IsPublicProperty(prop))
                            continue;
                        t.Members.Add(PropertySignature(prop));
                    }

                    foreach (var ev in type.Events)
                    {
                        if (!IsPublicEvent(ev))
                            continue;
                        t.Members.Add(EventSignature(ev));
                    }

                    manifest.Types.Add(t);
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(manifest, options);
            File.WriteAllText(outputPath, json);
            Console.WriteLine($"Wrote API manifest: {outputPath}");
            return 0;
        }

        private static bool IsPublicType(TypeDefinition t)
        {
            if (t.IsNested)
            {
                return t.IsNestedPublic;
            }
            return t.IsPublic;
        }

        private static bool IsPublicMethod(MethodDefinition m)
        {
            return m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
        }

        private static bool IsPublicField(FieldDefinition f)
        {
            return f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;
        }

        private static bool IsPublicProperty(PropertyDefinition p)
        {
            var g = p.GetMethod;
            var s = p.SetMethod;
            return (g != null && IsPublicMethod(g)) || (s != null && IsPublicMethod(s));
        }

        private static bool IsPublicEvent(EventDefinition e)
        {
            var ad = e.AddMethod;
            return ad != null && IsPublicMethod(ad);
        }

        private static string GetTypeKind(TypeDefinition t)
        {
            if (t.IsEnum) return "enum";
            if (t.IsValueType && !t.IsEnum) return "struct";
            if (t.IsInterface) return "interface";
            return "class";
        }

        private static string MethodSignature(MethodDefinition m)
        {
            var name = m.Name;
            if (m.HasGenericParameters)
                name += "<" + string.Join(",", m.GenericParameters.Select(g => g.Name)) + ">";
            var parameters = string.Join(", ", m.Parameters.Select(p => TypeRefName(p.ParameterType) + " " + p.Name));
            return $"{TypeRefName(m.ReturnType)} {name}({parameters})";
        }

        private static string FieldSignature(FieldDefinition f)
        {
            return $"{TypeRefName(f.FieldType)} {f.Name}";
        }

        private static string PropertySignature(PropertyDefinition p)
        {
            var hasGet = p.GetMethod != null && IsPublicMethod(p.GetMethod);
            var hasSet = p.SetMethod != null && IsPublicMethod(p.SetMethod);
            var accessors = (hasGet ? "get; " : "") + (hasSet ? "set; " : "");
            return $"{TypeRefName(p.PropertyType)} {p.Name} {{ {accessors}}}".Trim();
        }

        private static string EventSignature(EventDefinition e)
        {
            return $"event {TypeRefName(e.EventType)} {e.Name}";
        }

        private static string TypeRefName(TypeReference tr)
        {
            if (tr is GenericInstanceType git)
            {
                return git.ElementType.FullName + "<" + string.Join(",", git.GenericArguments.Select(TypeRefName)) + ">";
            }
            if (tr is ArrayType at)
            {
                return TypeRefName(at.ElementType) + "[]";
            }
            if (tr is ByReferenceType br)
            {
                return TypeRefName(br.ElementType) + "&";
            }
            if (tr is GenericParameter gp)
            {
                return gp.Name;
            }
            return tr.FullName;
        }

        private static List<(string leftPrefix, string rightPrefix)> ParseMappingsArgOrFile(string arg)
        {
            var list = new List<(string leftPrefix, string rightPrefix)>();
            try
            {
                if (File.Exists(arg))
                {
                    if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // parse JSON config: { "namespaceMappings": ["A=>B"], "typeAliases": ["X=>Y"] }
                        var json = File.ReadAllText(arg);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("namespaceMappings", out var ns))
                        {
                            foreach (var el in ns.EnumerateArray())
                            {
                                var s = el.GetString();
                                if (string.IsNullOrEmpty(s)) continue;
                                var parts = s.Split(new[]{"=>"}, StringSplitOptions.None);
                                if (parts.Length == 2) list.Add((parts[0].Trim(), parts[1].Trim()));
                            }
                        }
                    }
                    else
                    {
                        foreach (var line in File.ReadAllLines(arg))
                        {
                            var l = line.Trim();
                            if (string.IsNullOrEmpty(l) || l.StartsWith("#")) continue;
                            var parts = l.Split(new[]{"=>"}, StringSplitOptions.None);
                            if (parts.Length == 2) list.Add((parts[0].Trim(), parts[1].Trim()));
                        }
                    }
                }
                else
                {
                    foreach (var part in arg.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = part.Split(new[]{"=>"}, StringSplitOptions.None);
                        if (parts.Length == 2) list.Add((parts[0].Trim(), parts[1].Trim()));
                    }
                }
            }
            catch
            {
                // ignore
            }
            return list;
        }

        private static Dictionary<string,string> ParseTypeAliasesArgOrFile(string arg)
        {
            var dict = new Dictionary<string,string>();
            try
            {
                if (string.IsNullOrEmpty(arg)) return dict;
                if (File.Exists(arg))
                {
                    if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var json = File.ReadAllText(arg);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("typeAliases", out var ta))
                        {
                            foreach (var el in ta.EnumerateArray())
                            {
                                var s = el.GetString();
                                if (string.IsNullOrEmpty(s)) continue;
                                var p = s.Split(new[]{"=>"}, StringSplitOptions.None);
                                if (p.Length==2) dict[p[0].Trim()] = p[1].Trim();
                            }
                        }
                    }
                    else
                    {
                        foreach (var line in File.ReadAllLines(arg))
                        {
                            var l = line.Trim();
                            if (string.IsNullOrEmpty(l) || l.StartsWith("#")) continue;
                            var parts = l.Split(new[]{"=>"}, StringSplitOptions.None);
                            if (parts.Length==2) dict[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
                else
                {
                    // inline semicolon-separated
                    foreach (var part in arg.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var p = part.Split(new[]{"=>"}, StringSplitOptions.None);
                        if (p.Length==2) dict[p[0].Trim()] = p[1].Trim();
                    }
                }
            }
            catch
            {
            }
            return dict;
        }

        private static string MapFullName(string leftNamespace, string name, List<(string leftPrefix, string rightPrefix)> mappings)
        {
            if (mappings == null || mappings.Count == 0)
                return string.IsNullOrEmpty(leftNamespace) ? name : leftNamespace + "." + name;

            foreach (var m in mappings)
            {
                if (!string.IsNullOrEmpty(m.leftPrefix) && leftNamespace != null && leftNamespace.StartsWith(m.leftPrefix))
                {
                    var suffix = leftNamespace.Substring(m.leftPrefix.Length);
                    var mappedNs = m.rightPrefix + suffix;
                    return mappedNs + "." + name;
                }
            }

            return string.IsNullOrEmpty(leftNamespace) ? name : leftNamespace + "." + name;
        }
    }

    class ApiManifest
    {
        public ApiManifest()
        {
            Types = new List<ApiType>();
        }
        public List<ApiType> Types { get; set; }
    }

    class ApiType
    {
        public string Namespace { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "class";
        public List<string> Members { get; set; } = new();
    }
}
