using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;

namespace ApiDiff
{
    internal class Program
    {
    private static bool StrictPublic = false;
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
            else if (cmd == "validate-manifest")
            {
                return RunValidateManifest(args);
            }
            else if (cmd == "compare-assembly")
            {
                return RunCompareFromAssembly(args);
            }
            else if (cmd == "dump-type-vis")
            {
                return RunDumpTypeVis(args);
            }
            else if (cmd == "show-type-members")
            {
                return RunShowTypeMembers(args);
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
                Console.WriteLine("Usage: ApiDiff compare <left.api.json> <right.api.json> [mappings] [--ignore-mapped] [--left-only] [--left-only-file=path]");
                return 1;
            }

            var leftPath = args[1];
            var rightPath = args[2];
            var leftOnlyFlag = args.Any(a => a == "--left-only");
            StrictPublic = args.Any(a => a == "--strict-public");
            var leftOnlyMembersFlag = args.Any(a => a == "--left-only-members");
            var leftOnlyFileArg = args.FirstOrDefault(a => a.StartsWith("--left-only-file="));
            var ignoreMappedFlag = args.Any(a => a == "--ignore-mapped");
            // mappings can be provided as the 3rd positional arg or via --mappings=path
            var mappings = new List<(string leftPrefix, string rightPrefix)>();
            string mappingsSource = null;
            if (args.Length > 3 && !args[3].StartsWith("--"))
            {
                mappingsSource = args[3];
                mappings = ParseMappingsArgOrFile(mappingsSource);
            }
            var mappingsArgOpt = args.FirstOrDefault(a => a.StartsWith("--mappings="));
            if (mappingsArgOpt != null)
            {
                var parts = mappingsArgOpt.Split('=', 2);
                if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                {
                    mappingsSource = parts[1];
                    mappings = ParseMappingsArgOrFile(parts[1]);
                }
            }

            // also attempt to parse explicit type aliases from the same mappings source (if provided)
            var parsedTypeAliases = new Dictionary<string,string>();
            if (!string.IsNullOrEmpty(mappingsSource))
            {
                parsedTypeAliases = ParseTypeAliasesArgOrFile(mappingsSource);
            }
            string leftOnlyFile = null;
            if (leftOnlyFileArg != null)
            {
                var parts = leftOnlyFileArg.Split('=', 2);
                if (parts.Length == 2) leftOnlyFile = parts[1];
            }
            // If a built assembly exists with the same base name, validate the manifest against it to avoid stale-manifest compares.
            try
            {
                var leftBase = Path.GetFileNameWithoutExtension(leftPath);
                var repoRoot = Directory.GetCurrentDirectory();
                // look under common artifact dirs for a matching DLL
                var candidates = Directory.EnumerateFiles(repoRoot, leftBase + ".dll", SearchOption.AllDirectories)
                    .Where(p => p.IndexOf(Path.Combine("thirdparty", "Avalonia.Controls.DataGrid"), StringComparison.OrdinalIgnoreCase) >= 0
                                || p.IndexOf(Path.Combine("thirdparty"), StringComparison.OrdinalIgnoreCase) >= 0
                                || p.IndexOf(Path.Combine("artifacts"), StringComparison.OrdinalIgnoreCase) >= 0
                                || p.IndexOf(Path.Combine("bin"), StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                if (candidates.Count > 0)
                {
                    var candidate = candidates.First();
                    Console.WriteLine($"Found assembly matching manifest basename: {candidate} — validating manifest visibility against this DLL to avoid stale manifests.");
                    var res = RunValidateManifest(new string[]{"validate-manifest", leftPath, candidate});
                    if (res != 0)
                    {
                        Console.WriteLine("Aborting compare due to manifest/assembly visibility mismatch. Export the manifest from the built DLL and re-run, or use compare-assembly to export and compare in one step.");
                        return 2;
                    }
                }
            }
            catch { /* ignore search errors and proceed */ }
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

            List<string> leftOnly;
        if (ignoreMappedFlag && ( (mappings != null && mappings.Count>0) || (parsedTypeAliases != null && parsedTypeAliases.Count>0)))
            {
                // exclude left types that map to a right full name via namespace mappings or explicit type aliases
                leftOnly = leftMap.Keys.Where(k => {
                    if (rightMap.ContainsKey(k)) return false;
                    var t = leftMap[k];
                    // namespace-based mapping
                    var mapped = MapFullName(t.Namespace, t.Name, mappings);
                    if (!string.IsNullOrEmpty(mapped) && rightMap.ContainsKey(mapped)) return false;
                    // explicit type-alias mapping: leftFull => rightFull
                    var leftFull = string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
            if (parsedTypeAliases != null && parsedTypeAliases.TryGetValue(leftFull, out var aliasMapped))
                    {
                        if (!string.IsNullOrEmpty(aliasMapped) && rightMap.ContainsKey(aliasMapped)) return false;
                    }
                    return true;
                }).OrderBy(k=>k).ToList();
            }
            else
            {
                leftOnly = leftMap.Keys.Except(rightMap.Keys).OrderBy(k=>k).ToList();
            }
            var rightOnly = rightMap.Keys.Except(leftMap.Keys).OrderBy(k=>k).ToList();
            var both = leftMap.Keys.Intersect(rightMap.Keys).OrderBy(k=>k).ToList();

            // If requested, emit left-only list and exit (machine-friendly mode)
            if (leftOnlyFlag)
            {
                if (!string.IsNullOrEmpty(leftOnlyFile))
                {
                    File.WriteAllLines(leftOnlyFile, leftOnly);
                    Console.WriteLine($"Wrote {leftOnly.Count} left-only types to: {leftOnlyFile}");
                }
                else
                {
                    foreach (var t in leftOnly) Console.WriteLine(t);
                }
                return 0;
            }

            Console.WriteLine($"Left types: {leftMap.Count}, Right types: {rightMap.Count}");
            Console.WriteLine($"Types only in left: {leftOnly.Count}");
            if (!leftOnlyFlag && !leftOnlyMembersFlag)
            {
                Console.WriteLine($"Types only in right: {rightOnly.Count}");
            }
            Console.WriteLine($"Types in both: {both.Count}");

            if (leftOnly.Any())
            {
                Console.WriteLine("\nSample types only in left:");
                foreach (var t in leftOnly.Take(20)) Console.WriteLine("  " + t);
            }

            if (!leftOnlyFlag && !leftOnlyMembersFlag)
            {
                if (rightOnly.Any())
                {
                    Console.WriteLine("\nSample types only in right:");
                    foreach (var t in rightOnly.Take(20)) Console.WriteLine("  " + t);
                }
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

            // If requested, print members present only in left for types that exist in both
            if (leftOnlyMembersFlag)
            {
                // optional type-aliases can be provided via --type-aliases=pathOrInline
                var typeAliasesArg = args.FirstOrDefault(a => a.StartsWith("--type-aliases="));
                var typeAliases = new Dictionary<string,string>();
                if (typeAliasesArg != null)
                {
                    var parts = typeAliasesArg.Split('=',2);
                    if (parts.Length==2) typeAliases = ParseTypeAliasesArgOrFile(parts[1]);
                }

                Console.WriteLine("\nMembers present only in LEFT for matched types (types exist in both):");
                foreach (var tn in both.OrderBy(x=>x))
                {
                    var l = leftMap[tn];
                    var r = rightMap[tn];

                    // build normalized member sets honoring type aliases and the existing NormalizePropertyLike/stripParamNames/short-type heuristics
                    Func<string,string> normalizeTypeNames = s =>
                    {
                        if (string.IsNullOrEmpty(s)) return s;
                        foreach (var kv in typeAliases) s = s.Replace(kv.Key, kv.Value);
                        return s;
                    };

                    string stripParamNames(string sig)
                    {
                        var i = sig.IndexOf('(');
                        if (i < 0) return sig;
                        var pre = sig.Substring(0, i+1);
                        var rest = sig.Substring(i+1);
                        var j = rest.LastIndexOf(')');
                        if (j < 0) return sig;
                        var paramList = rest.Substring(0, j);
                        var post = rest.Substring(j);
                        if (string.IsNullOrWhiteSpace(paramList)) return pre + post;
                        var parts = paramList.Split(',');
                        var newParts = parts.Select(p => {
                            var tokens = p.Trim().Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length == 0) return "";
                            if (tokens.Length == 1) return tokens[0];
                            var last = tokens.Last();
                            if (char.IsLower(last[0]) || (!last.Contains(".") && last.All(c => char.IsLetterOrDigit(c) || c=='_')))
                            {
                                return string.Join(' ', tokens.Take(tokens.Length-1));
                            }
                            return string.Join(' ', tokens);
                        });
                        var newParamList = string.Join(", ", newParts.Where(x=>!string.IsNullOrEmpty(x)));
                        return pre + newParamList + post;
                    }

                    string NormalizePropertyLike(string sig)
                    {
                        if (string.IsNullOrEmpty(sig)) return sig;
                        var braceOpen = sig.IndexOf('{');
                        var braceClose = sig.LastIndexOf('}');
                        if (braceOpen >= 0 && braceClose > braceOpen)
                        {
                            var before = sig.Substring(0, braceOpen).Trim();
                            return before + " { property }";
                        }
                        var parts = sig.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var typePart = parts[0];
                            var namePart = parts[1];
                            var suffixes = new[] { "Property", "StyledProperty", "AvaloniaProperty" };
                            foreach (var suf in suffixes)
                            {
                                if (namePart.EndsWith(suf, StringComparison.Ordinal))
                                {
                                    var baseName = namePart.Substring(0, namePart.Length - suf.Length);
                                    var innerType = typePart;
                                    var lt = typePart.IndexOf('<');
                                    var rt = typePart.LastIndexOf('>');
                                    if (lt >= 0 && rt > lt)
                                    {
                                        innerType = typePart.Substring(lt + 1, rt - lt - 1).Trim();
                                    }
                                    var bt = innerType.IndexOf('`');
                                    if (bt >= 0) innerType = innerType.Substring(0, bt);
                                    return innerType + " " + baseName + " { property }";
                                }
                            }
                        }
                        return sig;
                    }

                    var leftMembers = new HashSet<string>(l.Members.Select(m=>NormalizePropertyLike(stripParamNames(normalizeTypeNames(m)))));
                    var rightMembers = new HashSet<string>(r.Members.Select(m=>NormalizePropertyLike(stripParamNames(normalizeTypeNames(m)))));

                    var leftOnlyMembers = leftMembers.Except(rightMembers).OrderBy(x=>x).ToList();
                    if (leftOnlyMembers.Count>0)
                    {
                        Console.WriteLine($"{tn} -> left-only-members: {leftOnlyMembers.Count}");
                        foreach (var m in leftOnlyMembers.Take(200))
                        {
                            Console.WriteLine("  " + m);
                            // Try to find potential matches in the right side by simple heuristics: same name and same parameter arity
                            string memberName = m;
                            int arity = -1;
                            var pIdx = m.IndexOf('(');
                            if (pIdx >= 0)
                            {
                                memberName = m.Substring(0, pIdx).Trim();
                                var paramList = m.Substring(pIdx+1);
                                var rp = paramList.LastIndexOf(')');
                                if (rp>=0) paramList = paramList.Substring(0, rp);
                                if (string.IsNullOrWhiteSpace(paramList)) arity = 0;
                                else arity = paramList.Split(',').Length;
                            }
                            else
                            {
                                // property/event/field form: treat as arity 0
                                arity = 0;
                                // name is first token after type for property-like 'Type Name { property }' or 'event Type Name'
                                var tokens = m.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                                if (tokens.Length>=2) memberName = string.Join(' ', tokens.Skip( tokens.Length-2 ));
                                // simplify to last token
                                var tparts = memberName.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                                if (tparts.Length>0) memberName = tparts.Last();
                            }

                            // find candidates in rightMembers
                            var candidates = rightMembers.Where(rm => {
                                var rmName = rm;
                                var rp2 = rm.IndexOf('(');
                                if (rp2>=0) rmName = rm.Substring(0, rp2).Trim();
                                else
                                {
                                    var tokens2 = rm.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                                    if (tokens2.Length>=2) rmName = tokens2.Last();
                                }
                                if (string.IsNullOrEmpty(rmName)) return false;
                                if (!rmName.Contains(memberName.Split(' ').Last())) return false;
                                // compare arity if applicable
                                if (pIdx>=0 && rp2>=0)
                                {
                                    var params2 = rm.Substring(rp2+1);
                                    var rp3 = params2.LastIndexOf(')');
                                    if (rp3>=0) params2 = params2.Substring(0, rp3);
                                    var arity2 = string.IsNullOrWhiteSpace(params2) ? 0 : params2.Split(',').Length;
                                    return arity2 == arity;
                                }
                                return true;
                            }).Take(10).ToList();

                            if (candidates.Count>0)
                            {
                                Console.WriteLine("    ~ potential matches in right:");
                                foreach (var c in candidates) Console.WriteLine("      " + c);
                            }
                        }
                    }
                }

                return 0;
            }

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
            StrictPublic = args.Any(a => a == "--strict-public");
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

                    // Canonicalize both property signatures and backing-field (dependency/styled) forms
                    // to a type-insensitive token: "Name { property }". This ensures fields like
                    // "NameProperty" or "NameDependencyProperty" compare equal to an instance property
                    // "System.String Name { get; set; }" regardless of how the backing field encodes the type.
                    var braceOpen = sig.IndexOf('{');
                    if (braceOpen >= 0)
                    {
                        // property-like: extract the name token before the brace
                        var before = sig.Substring(0, braceOpen).Trim();
                        // take last token as the name (type may contain spaces for generics)
                        var tokens = before.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 1)
                        {
                            var name = tokens.Last();
                            return name + " { property }";
                        }
                        return before + " { property }";
                    }

                    // field-like: "... Type NameProperty" or "... Type NameDependencyProperty"
                    var parts = sig.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var namePart = parts[1];
                        var suffixes = new[] { "Property", "StyledProperty", "AvaloniaProperty", "DependencyProperty", "AttachedProperty" };
                        foreach (var suf in suffixes)
                        {
                            if (namePart.EndsWith(suf, StringComparison.Ordinal))
                            {
                                var baseName = namePart.Substring(0, namePart.Length - suf.Length);
                                return baseName + " { property }";
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

        private static int RunValidateManifest(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff validate-manifest <manifest.json> <assembly.dll>");
                return 1;
            }

            var manifestPath = args[1];
            var assemblyPath = args[2];
            if (!File.Exists(manifestPath))
            {
                Console.WriteLine($"Manifest not found: {manifestPath}");
                return 1;
            }
            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"Assembly not found: {assemblyPath}");
                return 1;
            }

            var manifest = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(manifestPath)) ?? new ApiManifest();
            var asm = AssemblyDefinition.ReadAssembly(assemblyPath);

            // build set of public full type names from the assembly
            string FullName(TypeDefinition t) => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
            var publicTypes = new HashSet<string>();
            foreach (var module in asm.Modules)
            {
                foreach (var t in module.Types)
                {
                    if (IsPublicType(t)) publicTypes.Add(FullName(t));
                }
            }

            var manifestTypes = manifest.Types.Select(t => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name).ToList();
            var notPublic = manifestTypes.Where(mt => !publicTypes.Contains(mt)).OrderBy(x=>x).ToList();

            if (notPublic.Count == 0)
            {
                Console.WriteLine("All manifest types are public in the assembly.");
                return 0;
            }

            Console.WriteLine("Manifest contains types that are NOT public in the provided assembly:");
            foreach (var t in notPublic) Console.WriteLine("  " + t);
            Console.WriteLine($"Count: {notPublic.Count}");
            return 2;
        }

        private static int RunCompareFromAssembly(string[] args)
        {
            // Usage: compare-assembly <left-assembly.dll> <right.api.json> [keywords] [ns-mappings-json]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff compare-assembly <left-assembly.dll> <right.api.json> [keywords] [ns-mappings]");
                return 1;
            }

            var leftAssembly = args[1];
            var rightManifest = args[2];

            if (!File.Exists(leftAssembly)) { Console.WriteLine("Left assembly not found: " + leftAssembly); return 1; }
            if (!File.Exists(rightManifest)) { Console.WriteLine("Right manifest not found: " + rightManifest); return 1; }

            var tempManifest = Path.Combine("tools","ApiDiff","manifests", Path.GetFileNameWithoutExtension(leftAssembly) + ".tmp.api.json");
            // export
            var exArgsList = new List<string>{"export", leftAssembly, tempManifest};
            if (args.Any(a => a == "--strict-public")) exArgsList.Add("--strict-public");
            var exArgs = exArgsList.ToArray();
            RunExport(exArgs);

            // call member-diff-related with the generated manifest as left
            var remaining = args.Skip(3).ToArray();
            var newArgs = new List<string>{ "member-diff-related", tempManifest, rightManifest };
            newArgs.AddRange(remaining);
            return RunMemberDiffRelated(newArgs.ToArray());
        }

        private static int RunDumpTypeVis(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ApiDiff dump-type-vis <assembly.dll> <TypeFullName> [TypeFullName ...]");
                return 1;
            }
            var asmPath = args[1];
            if (!File.Exists(asmPath)) { Console.WriteLine("Assembly not found: " + asmPath); return 1; }
            var asm = AssemblyDefinition.ReadAssembly(asmPath);
            var names = args.Skip(2).ToList();
            // build map by fullname
            var map = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
            foreach (var m in asm.Modules)
            foreach (var t in m.Types)
                map[string.IsNullOrEmpty(t.Namespace)? t.Name : t.Namespace + "." + t.Name] = t;

            foreach (var name in names)
            {
                if (!map.TryGetValue(name, out var td))
                {
                    Console.WriteLine($"Type not found in assembly: {name}");
                    continue;
                }
                Console.WriteLine($"{name}: IsPublic={td.IsPublic}, IsNestedPublic={td.IsNestedPublic}, IsNestedFamily={td.IsNestedFamily}, IsNestedFamilyOrAssembly={td.IsNestedFamilyOrAssembly}, Visibility={td.Attributes}");
            }
            return 0;
        }

        private static int RunShowTypeMembers(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: ApiDiff show-type-members <left.api.json> <right.api.json> <LeftTypeFullName> [--mappings=path] [--type-aliases=path]");
                return 1;
            }

            var leftPath = args[1];
            var rightPath = args[2];
            var typeName = args[3];
            var mappings = new List<(string leftPrefix, string rightPrefix)>();
            var mappingsArg = args.FirstOrDefault(a=>a.StartsWith("--mappings="));
            if (mappingsArg != null) mappings = ParseMappingsArgOrFile(mappingsArg.Split('=',2)[1]);
            var typeAliases = new Dictionary<string,string>();
            var aliasesArg = args.FirstOrDefault(a=>a.StartsWith("--type-aliases="));
            if (aliasesArg != null) typeAliases = ParseTypeAliasesArgOrFile(aliasesArg.Split('=',2)[1]);

            if (!File.Exists(leftPath) || !File.Exists(rightPath)) { Console.WriteLine("manifest missing"); return 1; }
            var left = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(leftPath)) ?? new ApiManifest();
            var right = JsonSerializer.Deserialize<ApiManifest>(File.ReadAllText(rightPath)) ?? new ApiManifest();

            string FullName(ApiType t) => string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
            var leftMap = left.Types.ToDictionary(FullName, t=>t);
            var rightMap = right.Types.ToDictionary(FullName, t=>t);

            if (!leftMap.ContainsKey(typeName)) { Console.WriteLine("Left type not found: " + typeName); return 1; }
            var lt = leftMap[typeName];

            var onlyLeftFlag = args.Any(a => a == "--only-left-members" || a == "--left-only-members");

            Console.WriteLine($"Members for {typeName}: ({lt.Members.Count} members)");
            foreach (var m in lt.Members.OrderBy(x=>x))
            {
                // normalized form
                var normalized = m;
                // try namespace mapping of the containing type
                var mappedTypeFull = MapFullName(lt.Namespace, lt.Name, mappings);
                var mappedRightType = right.Types.FirstOrDefault(t => (string.IsNullOrEmpty(t.Namespace)?t.Name:t.Namespace+"."+t.Name) == mappedTypeFull);
                bool matched = false;
                string matchedMember = null;
                if (mappedRightType != null)
                {
                    // try to find an exact member match after alias substitution
                    Func<string,string> applyAliases = s => {
                        if (string.IsNullOrEmpty(s)) return s;
                        foreach (var kv in typeAliases) s = s.Replace(kv.Key, kv.Value);
                        return s;
                    };
                    var normLeft = NormalizePropertyLike(m);
                    normLeft = applyAliases(normLeft);
                    var rightMembers = mappedRightType.Members.Select(rm => applyAliases(NormalizePropertyLike(rm))).ToList();
                    if (rightMembers.Contains(normLeft)) { matched = true; matchedMember = normLeft; }
                    else
                    {
                        // heuristics: same name + arity
                        var pIdx = m.IndexOf('(');
                        string nameOnly = pIdx>=0 ? m.Substring(0,pIdx).Trim() : m;
                        int arity = 0;
                        if (pIdx>=0)
                        {
                            var rp = m.LastIndexOf(')');
                            var paramList = m.Substring(pIdx+1, rp-pIdx-1);
                            arity = string.IsNullOrWhiteSpace(paramList) ? 0 : paramList.Split(',').Length;
                        }
                        foreach (var rm in mappedRightType.Members)
                        {
                            var rpIdx = rm.IndexOf('(');
                            var rName = rpIdx>=0 ? rm.Substring(0,rpIdx).Trim() : rm;
                            int rArity = 0;
                            if (rpIdx>=0)
                            {
                                var rr = rm.LastIndexOf(')');
                                var paramList2 = rm.Substring(rpIdx+1, rr-rpIdx-1);
                                rArity = string.IsNullOrWhiteSpace(paramList2) ? 0 : paramList2.Split(',').Length;
                            }
                            if (rName.Contains(nameOnly.Split(' ').Last()) && rArity==arity)
                            {
                                matched = true; matchedMember = rm; break;
                            }
                        }
                    }
                }

                if (onlyLeftFlag)
                {
                    if (!matched)
                        Console.WriteLine(m);
                }
                else
                {
                    Console.WriteLine((matched?"[MAPPED] ":"[LEFT ] ") + m + (matched?"  -> " + matchedMember : ""));
                }
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

            // honor strict-public during export: only include truly public (non-protected) members
            StrictPublic = args.Any(a => a == "--strict-public");

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
            if (StrictPublic) return m.IsPublic;
            return m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
        }

        private static bool IsPublicField(FieldDefinition f)
        {
            if (StrictPublic) return f.IsPublic;
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

        private static string NormalizePropertyLike(string sig)
        {
            if (string.IsNullOrEmpty(sig)) return sig;

            var braceOpen = sig.IndexOf('{');
            if (braceOpen >= 0)
            {
                var before = sig.Substring(0, braceOpen).Trim();
                var tokens = before.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 1)
                {
                    var name = tokens.Last();
                    return name + " { property }";
                }
                return before + " { property }";
            }

            var parts = sig.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var namePart = parts[1];
                var suffixes = new[] { "Property", "StyledProperty", "AvaloniaProperty", "DependencyProperty", "AttachedProperty" };
                foreach (var suf in suffixes)
                {
                    if (namePart.EndsWith(suf, StringComparison.Ordinal))
                    {
                        var baseName = namePart.Substring(0, namePart.Length - suf.Length);
                        return baseName + " { property }";
                    }
                }
            }

            return sig;
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
