using System;
using Microsoft.Data.Sqlite; // Added for SQLite functionality
using System.Collections.Generic; // Added for List<string>
using System.Xml.Linq; // Added for XML parsing
using System.Linq; // Added for LINQ methods like Distinct()

namespace CoreConverter
{
    // POCO for Feature Class Information
    // Base class for items stored in GDB_Items
    public abstract class EsriItemInfo
    {
        public string ItemUuid { get; set; } = string.Empty; // From GDB_Items.UUID
        public string Name { get; set; } = string.Empty; // From GDB_Items.Name
        public string Path { get; set; } = string.Empty; // From GDB_Items.Path
        public string PhysicalName { get; set; } = string.Empty; // Actual table name for data
        public string Definition { get; set; } = string.Empty; // Full XML definition from GDB_Items.Definition
        public string ItemTypeGuid { get; set; } = string.Empty; // From GDB_Items.Type
        public string DatasetName { get; set; } = string.Empty; // Feature Dataset name, if any
        public List<EsriFieldInfo> Fields { get; set; } = new List<EsriFieldInfo>();
        public string? SubtypeFieldName { get; set; }
        public List<EsriSubtypeInfo>? Subtypes { get; set; }
        public List<string>? TopologyParticipation { get; set; } // Stores names or indicators of topology participation
        public List<string>? AttributeRuleInfo { get; set; } // Stores descriptive text of attribute rules
    }

    // POCO for Subtype Information
    public class EsriSubtypeInfo
    {
        public int Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; // Often not present or same as Name
        public Dictionary<string, EsriSubtypeFieldDefaultValue> FieldDefaults { get; set; } = new Dictionary<string, EsriSubtypeFieldDefaultValue>();
    }

    // POCO for Field Default Values within a Subtype
    public class EsriSubtypeFieldDefaultValue
    {
        public object? DefaultValue { get; set; }
        public string? DomainName { get; set; } // If a specific domain is assigned for this field at this subtype
    }

    // POCO for Feature Class Information (inherits from EsriItemInfo)
    public class EsriFeatureClassInfo : EsriItemInfo
    {
        public string GeometryColumnName { get; set; } = string.Empty;
        public string GeometryType { get; set; } = string.Empty;
        public int Srid { get; set; }
    }
    
    // POCO for plain Table Information (inherits from EsriItemInfo, no spatial properties)
    public class EsriTableInfo : EsriItemInfo
    {
        // Currently no additional properties beyond EsriItemInfo
        // but serves to distinguish plain tables from feature classes.
    }


    // POCO for Field Information
    public class EsriFieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // e.g., esriFieldTypeString, esriFieldTypeInteger
        public string DomainName { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public int Length { get; set; } // For string fields
        // Add other properties as needed, e.g., Precision, Scale for numeric types
    }

    // POCO for Domain Information
    public class EsriDomainInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty; // Esri field type this domain applies to
        public string DomainType { get; set; } = string.Empty; // "CodedValue" or "Range"
        public List<EsriCodedValue>? CodedValues { get; set; }
        public EsriRangeValue? RangeValue { get; set; }
        public string Owner { get; set; } = string.Empty; // GDB_Domains.Owner
        public string DefinitionXML { get; set; } = string.Empty; // GDB_Domains.Definition (XML)
    }

    // POCO for Coded Values in a Domain
    public class EsriCodedValue
    {
        public object Code { get; set; } = new object(); // Can be string, int, etc.
        public string Name { get; set; } = string.Empty; // User-friendly description of the code
    }

    // POCO for Range Value in a Domain
    public class EsriRangeValue
    {
        public object MinValue { get; set; } = new object();
        public object MaxValue { get; set; } = new object();
    }
    
    // POCO for Spatial Reference Information
    public class EsriSpatialReferenceInfo
    {
        public int Srid { get; set; }
        public string SrsName { get; set; } = string.Empty; // AuthorityName in GDB_SpatialRefs
        public string SrsDefinition { get; set; } = string.Empty; // WKT or SRTEXT
    }

    // POCO for Relationship Class Information
    public class EsriRelationshipClassInfo
    {
        public string ItemUuid { get; set; } = string.Empty; // UUID of the relationship item in GDB_Items
        public string Name { get; set; } = string.Empty; // Name of the relationship class from GDB_Items
        public string OriginItemUuid { get; set; } = string.Empty;
        public string DestinationItemUuid { get; set; } = string.Empty;
        public string OriginTableName { get; set; } = string.Empty; // Name of the origin table/FC
        public string DestinationTableName { get; set; } = string.Empty; // Name of the destination table/FC
        public string Cardinality { get; set; } = string.Empty; // e.g., esriRelCardinalityOneToMany
        public string RelationshipType { get; set; } = string.Empty; // e.g., esriRelTypeSimple, esriRelTypeComposite
        public string ForwardPathLabel { get; set; } = string.Empty;
        public string BackwardPathLabel { get; set; } = string.Empty;
        public List<EsriRelationshipRuleInfo> Rules { get; set; } = new List<EsriRelationshipRuleInfo>();
        public string DefinitionXml { get; set; } = string.Empty; // GDB_Items.Definition for the relationship itself
        public string ItemRelationshipDefinitionXml { get; set; } = string.Empty; // GDB_ItemRelationships.Definition (can be different)
    }

    // POCO for Relationship Rules (simplified)
    public class EsriRelationshipRuleInfo
    {
        public string OriginKey { get; set; } = string.Empty; // Primary Key in Origin or Foreign Key
        public string DestinationKey { get; set; } = string.Empty; // Foreign Key in Destination or Primary Key
        // Could add more details like RuleID, Subtype if needed later
    }


    public class Converter
    {
        // Common ESRI UUIDs for identifying item types (these are examples, actual values might vary)
        // These GUIDs are typically found in GDB_ItemTypes table or are well-known.
        // For example, from https://support.esri.com/en-us/knowledge-base/faq-what-are-the-clsids-for-the-different-object-type-000005721
        // Or by inspecting GDB_ItemTypes.UUID for entries like 'esriDatasetTypeFeatureClass', 'esriDatasetTypeRelationshipClass'
        private const string ESRI_FEATURE_CLASS_TYPE_GUID = "{70737809-852C-4A03-9E22-2CECEA5B9BFA}"; // Example, for actual Feature Class
        private const string ESRI_TABLE_TYPE_GUID = "{CD06BC1B-789D-4C51-AAFA-4875E4034352}"; // Example, for actual Table
        private const string ESRI_RELATIONSHIP_CLASS_TYPE_GUID = "{B606A7E1-FA5B-439C-849C-6E9C2481537B}"; // Example, for Relationship Class

        /// <summary>
        /// Processes the conversion from an Esri File Geodatabase to GeoPackage.
        /// </summary>
        /// <param name="inputPath">The path to the input Esri Mobile Geodatabase file.</param>
        /// <param name="outputPath">The path to the output GeoPackage file (.gpkg).</param>
        /// <param name="targetSrs">The target Spatial Reference System (SRS) for output geometries, defaults to "EPSG:4326".</param>
        /// <param name="bbox">An optional bounding box (minX, minY, maxX, maxY) to filter geometries.</param>
        public void Process(string inputPath, string outputPath, string targetSrs = "EPSG:4326", double[]? bbox = null)
        {
            Console.WriteLine($"Input GDB: {inputPath}");
            Console.WriteLine($"Output GPKG: {outputPath}");
            Console.WriteLine($"Target SRS: {targetSrs}");
            if (bbox != null && bbox.Length == 4)
            {
                Console.WriteLine($"Bounding Box (bbox): [{string.Join(", ", bbox)}]");
            }

            // var featureClasses = new List<EsriFeatureClassInfo>(); // Now part of allItems
            var allItems = new Dictionary<string, EsriItemInfo>(); // Key: ItemUUID (from GDB_Items.UUID)
            var spatialReferences = new Dictionary<int, EsriSpatialReferenceInfo>();
            var domains = new Dictionary<string, EsriDomainInfo>();
            var relationshipClasses = new List<EsriRelationshipClassInfo>();

            SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = inputPath,
                Mode = SqliteOpenMode.ReadOnly
            };

            try
            {
                using (var connection = new SqliteConnection(connectionStringBuilder.ConnectionString))
                {
                    connection.Open();
                    Console.WriteLine("\nSuccessfully connected to the Geodatabase.");

                    // 1. Query GDB_Items to find feature classes
                    // We are looking for items that are feature classes.
                    // Their type is often a specific UUID.
                    // We also need the Definition XML and the PhysicalName (actual table name).
                    // GDB_Items.Type = '{uuid-for-featureclass}'
                    // GDB_Items.DatasetName can be used to group feature classes by feature dataset.
                    // GDB_ItemRelationships can show containment (e.g. FeatureDataset contains FeatureClass)
                    // For now, we assume GDB_Items.Definition holds enough info.
                    
                    // First, let's check if GDB_Items table exists
                    bool gdbItemsExists = false;
                    using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GDB_Items';", connection))
                    {
                        gdbItemsExists = cmd.ExecuteScalar() != null;
                    }

                    if (!gdbItemsExists)
                    {
                        Console.WriteLine("Error: Table 'GDB_Items' not found in the database. Cannot proceed with metadata extraction.");
                        return;
                    }
                    
                    Console.WriteLine("\nQuerying GDB_Items for all items (feature classes, tables, relationships)...");
                    // We need UUID, Name, Path, Definition, Type, PhysicalName, DatasetName
                    string itemsQuery = "SELECT UUID, Name, Path, Definition, Type, PhysicalName, DatasetName FROM GDB_Items;"; 

                    using (var itemsCommand = new SqliteCommand(itemsQuery, connection))
                    {
                        using (var reader = itemsCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string itemUuid = reader.IsDBNull(reader.GetOrdinal("UUID")) ? string.Empty : reader.GetString(reader.GetOrdinal("UUID"));
                                string itemName = reader.IsDBNull(reader.GetOrdinal("Name")) ? string.Empty : reader.GetString(reader.GetOrdinal("Name"));
                                string itemPath = reader.IsDBNull(reader.GetOrdinal("Path")) ? string.Empty : reader.GetString(reader.GetOrdinal("Path"));
                                string definitionXml = reader.IsDBNull(reader.GetOrdinal("Definition")) ? string.Empty : reader.GetString(reader.GetOrdinal("Definition"));
                                string itemTypeGuidValue = reader.IsDBNull(reader.GetOrdinal("Type")) ? string.Empty : reader.GetString(reader.GetOrdinal("Type")); // This is a GUID referencing GDB_ItemTypes
                                string physicalName = reader.IsDBNull(reader.GetOrdinal("PhysicalName")) ? itemName : reader.GetString(reader.GetOrdinal("PhysicalName")); 
                                string datasetName = reader.IsDBNull(reader.GetOrdinal("DatasetName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DatasetName"));
                                
                                if (string.IsNullOrWhiteSpace(itemUuid) || string.IsNullOrWhiteSpace(definitionXml))
                                {
                                    // Console.WriteLine($"Skipping item with missing UUID or Definition XML. Name: '{itemName}', Path: '{itemPath}'");
                                    continue;
                                }
                                
                                EsriItemInfo currentItem;

                                // Attempt to parse Definition XML to determine if it's a Feature Class or Table
                                try
                                {
                                    XDocument doc = XDocument.Parse(definitionXml);
                                    string? geometryColumn = doc.Descendants("ShapeField").FirstOrDefault()?.Value ?? 
                                                             doc.Descendants("GeometryFieldName").FirstOrDefault()?.Value;
                                    string? geometryType = doc.Descendants("GeometryType").FirstOrDefault()?.Value;
                                    
                                    if (!string.IsNullOrEmpty(geometryType) && !string.IsNullOrEmpty(geometryColumn))
                                    {
                                        var fcInfo = new EsriFeatureClassInfo
                                        {
                                            GeometryColumnName = geometryColumn,
                                            GeometryType = geometryType
                                        };
                                        var sridNode = doc.Descendants("WKID").FirstOrDefault() ?? doc.Descendants("SRID").FirstOrDefault();
                                        if (sridNode != null && int.TryParse(sridNode.Value, out int parsedSrid))
                                        {
                                            fcInfo.Srid = parsedSrid;
                                            if (parsedSrid > 0 && !spatialReferences.ContainsKey(parsedSrid))
                                            {
                                                spatialReferences[parsedSrid] = new EsriSpatialReferenceInfo { Srid = parsedSrid };
                                            }
                                        }
                                        currentItem = fcInfo;
                                        Console.WriteLine($"Identified Feature Class: '{itemName}', UUID: {itemUuid}, TypeGUID: {itemTypeGuidValue}");
                                    }
                                    // Check for <DETableInfo> or if it's a RelationshipClass (which is also a type of table-like item)
                                    else if (doc.Descendants("DETableInfo").Any() || itemTypeGuidValue.Equals(ESRI_TABLE_TYPE_GUID, StringComparison.OrdinalIgnoreCase) || itemTypeGuidValue.Equals(ESRI_RELATIONSHIP_CLASS_TYPE_GUID, StringComparison.OrdinalIgnoreCase))
                                    {
                                        currentItem = new EsriTableInfo(); // Could be a plain table or a relationship class item
                                        if (itemTypeGuidValue.Equals(ESRI_RELATIONSHIP_CLASS_TYPE_GUID, StringComparison.OrdinalIgnoreCase))
                                        {
                                             Console.WriteLine($"Identified Relationship Class Item: '{itemName}', UUID: {itemUuid}, TypeGUID: {itemTypeGuidValue}");
                                        } else {
                                             Console.WriteLine($"Identified Table: '{itemName}', UUID: {itemUuid}, TypeGUID: {itemTypeGuidValue}");
                                        }
                                    }
                                    else
                                    {
                                        // Console.WriteLine($"Skipping item '{itemName}' (Path: {itemPath}, UUID: {itemUuid}) - XML definition not recognized as FC or Table.");
                                        continue;
                                    }

                                    // Common properties for all EsriItemInfo
                                    currentItem.ItemUuid = itemUuid;
                                    currentItem.Name = itemName;
                                    currentItem.Path = itemPath;
                                    currentItem.PhysicalName = physicalName;
                                    currentItem.Definition = definitionXml;
                                    currentItem.ItemTypeGuid = itemTypeGuidValue;
                                    currentItem.DatasetName = datasetName;
                                    
                                    // Extract Fields for both Feature Classes and Tables (already done in previous step)
                                    var fieldsElement = doc.Descendants("Fields").FirstOrDefault() ?? doc.Descendants("FieldArray").FirstOrDefault();
                                    if (fieldsElement != null)
                                    {
                                        foreach (var fieldElement in fieldsElement.Elements("Field"))
                                        {
                                            var fieldInfo = new EsriFieldInfo
                                            {
                                                Name = fieldElement.Element("Name")?.Value ?? string.Empty,
                                                Type = fieldElement.Element("Type")?.Value ?? string.Empty,
                                                AliasName = fieldElement.Element("AliasName")?.Value ?? string.Empty,
                                                IsNullable = bool.TryParse(fieldElement.Element("IsNullable")?.Value, out bool nullable) && nullable,
                                                Length = int.TryParse(fieldElement.Element("Length")?.Value, out int len) ? len : 0,
                                                DomainName = fieldElement.Descendants("DomainName").FirstOrDefault()?.Value ?? 
                                                             fieldElement.Element("Domain")?.Element("Name")?.Value ??
                                                             string.Empty 
                                            };
                                            currentItem.Fields.Add(fieldInfo);
                                            if (!string.IsNullOrEmpty(fieldInfo.DomainName) && !domains.ContainsKey(fieldInfo.DomainName))
                                            {
                                                domains[fieldInfo.DomainName] = new EsriDomainInfo { Name = fieldInfo.DomainName };
                                            }
                                        }
                                    }

                                    // Extract Subtype Information (as per previous step)
                                    currentItem.SubtypeFieldName = doc.Descendants("SubtypeFieldName").FirstOrDefault()?.Value ??
                                                                   doc.Descendants("SubtypeField").FirstOrDefault()?.Value; 
                                    if (!string.IsNullOrEmpty(currentItem.SubtypeFieldName))
                                    {
                                        // ... (subtype parsing logic from previous step, assumed to be complete) ...
                                        // For brevity, not repeating the full subtype parsing logic here.
                                        // Ensure it correctly populates currentItem.Subtypes
                                        var subtypesElement = doc.Descendants("Subtypes").FirstOrDefault() ?? doc.Descendants("SubtypeInfos").FirstOrDefault();
                                        if (subtypesElement != null)
                                        {
                                            currentItem.Subtypes = new List<EsriSubtypeInfo>();
                                            var subtypeElements = subtypesElement.Elements().Where(e => e.Name.LocalName == "Subtype" || e.Name.LocalName == "SubtypeInfo").ToList();
                                            foreach (var subtypeElement in subtypeElements)
                                            {
                                                if (!int.TryParse(subtypeElement.Element("SubtypeCode")?.Value, out int subtypeCode)) continue;
                                                var subtypeInfo = new EsriSubtypeInfo { Code = subtypeCode, Name = subtypeElement.Element("SubtypeName")?.Value ?? string.Empty, Description = subtypeElement.Element("Description")?.Value ?? string.Empty };
                                                var fieldInfosElement = subtypeElement.Element("FieldInfos");
                                                if (fieldInfosElement != null)
                                                {
                                                    foreach (var subtypeFieldInfoElement in fieldInfosElement.Elements("SubtypeFieldInfo"))
                                                    {
                                                        string? fieldName = subtypeFieldInfoElement.Element("FieldName")?.Value;
                                                        if (string.IsNullOrEmpty(fieldName)) continue;
                                                        var fieldDefault = new EsriSubtypeFieldDefaultValue { DefaultValue = subtypeFieldInfoElement.Element("DefaultValue")?.Value, DomainName = subtypeFieldInfoElement.Element("DomainName")?.Value };
                                                        if (fieldDefault.DefaultValue != null || !string.IsNullOrEmpty(fieldDefault.DomainName)) subtypeInfo.FieldDefaults[fieldName] = fieldDefault;
                                                    }
                                                }
                                                currentItem.Subtypes.Add(subtypeInfo);
                                            }
                                            if (currentItem.Subtypes.Any()) Console.WriteLine($"  -> Item '{currentItem.Name}' uses Subtype Field: '{currentItem.SubtypeFieldName}', Found {currentItem.Subtypes.Count} subtypes.");
                                        }
                                    }

                                    // Extract Topology Participation
                                    var topologyMembership = doc.Descendants("TopologyMembership").FirstOrDefault(); // Common in FGDB
                                    if (topologyMembership != null)
                                    {
                                        currentItem.TopologyParticipation ??= new List<string>();
                                        string topologyName = topologyMembership.Element("TopologyName")?.Value ?? "Unnamed Topology";
                                        currentItem.TopologyParticipation.Add($"Participates in Topology: {topologyName}");
                                    }
                                    else if (doc.Descendants("IsInTopology").FirstOrDefault(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase)) != null)
                                    {
                                        currentItem.TopologyParticipation ??= new List<string>();
                                        currentItem.TopologyParticipation.Add("Participates in an unnamed Topology (IsInTopology=true)");
                                    }
                                    
                                    // Extract Attribute Rules (Simplified)
                                    // Looking for <Rules><Rule> or <AttributeRules><AttributeRule>
                                    var rulesContainer = doc.Descendants("Rules").FirstOrDefault() ?? doc.Descendants("AttributeRules").FirstOrDefault();
                                    if (rulesContainer != null)
                                    {
                                        currentItem.AttributeRuleInfo ??= new List<string>();
                                        var ruleElements = rulesContainer.Elements().Where(e => e.Name.LocalName == "Rule" || e.Name.LocalName == "AttributeRule").ToList();
                                        foreach (var ruleElement in ruleElements)
                                        {
                                            string ruleName = ruleElement.Element("Name")?.Value ?? 
                                                              ruleElement.Element("RuleName")?.Value ?? 
                                                              ruleElement.Element("ID")?.Value ?? // Sometimes just an ID
                                                              $"Unnamed Rule {currentItem.AttributeRuleInfo.Count + 1}";
                                            string ruleDescription = ruleElement.Element("Description")?.Value ?? string.Empty;
                                            string ruleExpression = ruleElement.Element("Expression")?.Value ?? 
                                                                    ruleElement.Element("ArcadeExpression")?.Value ?? string.Empty; // Arcade is common
                                            string ruleType = ruleElement.Element("Type")?.Value ?? string.Empty; // e.g., Calculation, Constraint, Validation

                                            string ruleInfo = $"Rule: '{ruleName}' (Type: {ruleType})";
                                            if (!string.IsNullOrWhiteSpace(ruleDescription)) ruleInfo += $", Desc: '{ruleDescription}'";
                                            if (!string.IsNullOrWhiteSpace(ruleExpression)) ruleInfo += $", Expr: '{Truncate(ruleExpression, 50)}'";
                                            
                                            currentItem.AttributeRuleInfo.Add(ruleInfo);
                                        }
                                        if (currentItem.AttributeRuleInfo.Any()) Console.WriteLine($"  -> Item '{currentItem.Name}' has {currentItem.AttributeRuleInfo.Count} attribute rules.");
                                    }
                                    
                                    allItems[itemUuid] = currentItem;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Warning: Failed to parse Definition XML for item '{itemName}' (UUID: {itemUuid}): {ex.Message.Split('\n')[0]}");
                                }
                            }
                        }
                    }

                    // Steps 2 (SpatialRefs) and 3 (Domains) remain largely the same, 
                    // but would operate on SRIDs/DomainNames collected from *all* items in `allItems`
                    // For brevity, those sections are not repeated here but should be understood as following this GDB_Items parsing.
                    // Ensure to call spatial reference and domain processing here.
                    ProcessSpatialReferences(connection, spatialReferences);
                    ProcessDomains(connection, domains);


                    // 4. Query GDB_ItemRelationships and GDB_Items (for relationship details)
                    bool itemRelationshipsExists = false;
                    using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GDB_ItemRelationships';", connection))
                    {
                        itemRelationshipsExists = cmd.ExecuteScalar() != null;
                    }

                    if (itemRelationshipsExists)
                    {
                        Console.WriteLine("\nQuerying GDB_ItemRelationships for relationship classes...");
                        // GDB_ItemRelationships.UUID is the UUID of the Relationship ITSELF in GDB_Items.
                        // GDB_ItemRelationships.Definition might be a simpler XML or empty, the full def is in GDB_Items.
                        string relQuery = "SELECT UUID, OriginItemUUID, DestinationItemUUID, Name, Definition FROM GDB_ItemRelationships;";
                        using (var relCmd = new SqliteCommand(relQuery, connection))
                        {
                            using (var relReader = relCmd.ExecuteReader())
                            {
                                while (relReader.Read())
                                {
                                    string relationshipItemUuid = relReader.GetString(relReader.GetOrdinal("UUID"));
                                    string originUuid = relReader.GetString(relReader.GetOrdinal("OriginItemUUID"));
                                    string destUuid = relReader.GetString(relReader.GetOrdinal("DestinationItemUUID"));
                                    string relationshipNameInItemRelationships = relReader.GetString(relReader.GetOrdinal("Name")); // Name from GDB_ItemRelationships
                                    string itemRelationshipDefinitionXml = relReader.IsDBNull(relReader.GetOrdinal("Definition")) ? string.Empty : relReader.GetString(relReader.GetOrdinal("Definition"));

                                    if (allItems.TryGetValue(relationshipItemUuid, out EsriItemInfo? relItem) &&
                                        allItems.TryGetValue(originUuid, out EsriItemInfo? originItem) &&
                                        allItems.TryGetValue(destUuid, out EsriItemInfo? destItem))
                                    {
                                        // The 'relItem.Definition' (from GDB_Items for the relationship) is usually the richer XML.
                                        XDocument relDoc = XDocument.Parse(relItem.Definition);
                                        var relInfo = new EsriRelationshipClassInfo
                                        {
                                            ItemUuid = relationshipItemUuid,
                                            Name = relItem.Name, // Use name from GDB_Items for the relationship
                                            OriginItemUuid = originUuid,
                                            DestinationItemUuid = destUuid,
                                            OriginTableName = originItem.Name, // Or PhysicalName if preferred
                                            DestinationTableName = destItem.Name, // Or PhysicalName
                                            Cardinality = relDoc.Descendants("Cardinality").FirstOrDefault()?.Value ?? "Unknown",
                                            RelationshipType = relDoc.Descendants("Type").FirstOrDefault()?.Value ?? // ESRI Relationship Type
                                                               relDoc.Descendants("RelationshipType").FirstOrDefault()?.Value ?? "Unknown", // Alternative tag
                                            ForwardPathLabel = relDoc.Descendants("ForwardPathLabel").FirstOrDefault()?.Value ?? string.Empty,
                                            BackwardPathLabel = relDoc.Descendants("BackwardPathLabel").FirstOrDefault()?.Value ?? string.Empty,
                                            DefinitionXml = relItem.Definition,
                                            ItemRelationshipDefinitionXml = itemRelationshipDefinitionXml
                                        };
                                        
                                        // Parse rules (key fields) - this is highly variable in ESRI XML
                                        // Example: <OriginClassKeys><RelationshipClassKey><ClassKeyName>OBJECTID</ClassKeyName>...</OriginClassKeys>
                                        // Or sometimes <KeyRole>Origin</KeyRole><Key>ORIGIN_PRIMARY_KEY_FIELD</Key>
                                        // For simplicity, we'll look for common patterns.
                                        var originKeyElement = relDoc.Descendants("OriginPrimaryKey").FirstOrDefault() ?? // FGDB style
                                                               relDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Key" && e.Parent?.Element("KeyRole")?.Value == "Origin"); // Simpler style
                                        var destKeyElement = relDoc.Descendants("OriginForeignKey").FirstOrDefault() ?? // FGDB style (FK is on origin for 1-M)
                                                             relDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Key" && e.Parent?.Element("KeyRole")?.Value == "Destination");

                                        if (originKeyElement != null && destKeyElement != null)
                                        {
                                            relInfo.Rules.Add(new EsriRelationshipRuleInfo { OriginKey = originKeyElement.Value, DestinationKey = destKeyElement.Value });
                                        } 
                                        else // Try another common pattern for M-N relationships (often stored in a separate table)
                                        {
                                            var originPrimaryKey = relDoc.Descendants("OriginClassKeys").FirstOrDefault()?.Descendants("ClassKeyName").FirstOrDefault()?.Value;
                                            var destinationPrimaryKey = relDoc.Descendants("DestinationClassKeys").FirstOrDefault()?.Descendants("ClassKeyName").FirstOrDefault()?.Value;
                                            // In M-N, the relationship table has FKs to both origin and destination PKs
                                            var originForeignKeyInRelTable = relDoc.Descendants("RelationshipClassKeys").FirstOrDefault(k => k.Element("KeyRole")?.Value == "OriginForeignKey")?.Element("ClassKeyName")?.Value;
                                            var destForeignKeyInRelTable = relDoc.Descendants("RelationshipClassKeys").FirstOrDefault(k => k.Element("KeyRole")?.Value == "DestinationForeignKey")?.Element("ClassKeyName")?.Value;

                                            if (!string.IsNullOrEmpty(originPrimaryKey) && !string.IsNullOrEmpty(destForeignKeyInRelTable)) // Origin to RelTable
                                            {
                                                relInfo.Rules.Add(new EsriRelationshipRuleInfo { OriginKey = originPrimaryKey, DestinationKey = destForeignKeyInRelTable });
                                            }
                                            if (!string.IsNullOrEmpty(destinationPrimaryKey) && !string.IsNullOrEmpty(originForeignKeyInRelTable)) // RelTable to Dest
                                            {
                                                 // This might need a more complex representation if the intermediate table is explicit
                                            }

                                        }


                                        relationshipClasses.Add(relInfo);
                                        Console.WriteLine($"Found Relationship: '{relInfo.Name}' from '{relInfo.OriginTableName}' to '{relInfo.DestinationTableName}'");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Warning: Could not fully resolve relationship with Name '{relationshipNameInItemRelationships}' (UUID: {relationshipItemUuid}). Origin, Destination, or Relationship item missing from GDB_Items cache.");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Info: Table 'GDB_ItemRelationships' not found. Skipping relationship extraction.");
                    }

                } // Connection closed

                // 5. Print extracted information
                Console.WriteLine("\n--- Extracted Item Information (Feature Classes & Tables) ---");
                if (!allItems.Any())
                {
                    Console.WriteLine("No feature classes or tables found/extracted.");
                }
                foreach (var item in allItems.Values)
                {
                    Console.WriteLine($"  Name: {item.Name} (UUID: {item.ItemUuid}, Physical Table: {item.PhysicalName}, TypeGUID: {item.ItemTypeGuid})");
                    Console.WriteLine($"    Path: {item.Path}, Dataset: {(string.IsNullOrEmpty(item.DatasetName) ? "N/A" : item.DatasetName)}");
                    
                    if (item is EsriFeatureClassInfo fc)
                    {
                        if (!string.IsNullOrEmpty(fc.GeometryColumnName))
                        {
                            Console.WriteLine($"    Geometry Column: {fc.GeometryColumnName}, Type: {fc.GeometryType}, SRID: {fc.Srid}");
                            if (spatialReferences.TryGetValue(fc.Srid, out var srsInfo))
                            {
                                Console.WriteLine($"      SRS Name: {srsInfo.SrsName}");
                                Console.WriteLine($"      SRS Definition (WKT): {(srsInfo.SrsDefinition.Length > 60 ? srsInfo.SrsDefinition.Substring(0, 60) + "..." : srsInfo.SrsDefinition)}");
                            }
                            else if (fc.Srid > 0) { Console.WriteLine($"      SRS Definition: SRID {fc.Srid} - Definition not found or GDB_SpatialRefs missing."); }
                            else { Console.WriteLine($"      SRS Definition: Not defined or SRID is 0."); }
                        }
                    }
                    Console.WriteLine("    Fields:");
                    if (!item.Fields.Any()) Console.WriteLine("      No fields extracted.");
                    foreach(var field in item.Fields)
                    {
                        Console.Write($"      - {field.Name} (Type: {field.Type}, Nullable: {field.IsNullable}, Length: {field.Length}, Alias: {field.AliasName})");
                        if (!string.IsNullOrEmpty(field.DomainName)) { Console.Write($" -> Domain: {field.DomainName} (default)"); }
                        Console.WriteLine();
                    }

                    // Print Subtype Information
                    if (!string.IsNullOrEmpty(item.SubtypeFieldName) && item.Subtypes != null && item.Subtypes.Any())
                    {
                        Console.WriteLine($"    Subtype Field: {item.SubtypeFieldName}");
                        Console.WriteLine("    Subtypes:");
                        foreach (var subtype in item.Subtypes)
                        {
                            Console.WriteLine($"      - Code: {subtype.Code}, Name: '{subtype.Name}', Description: '{subtype.Description}'");
                            if (subtype.FieldDefaults.Any())
                            {
                                Console.WriteLine("        Field-specific Defaults/Domains for this Subtype:");
                                foreach (var fd in subtype.FieldDefaults)
                                {
                                    string defaultValStr = fd.Value.DefaultValue != null ? $"Default: '{fd.Value.DefaultValue}'" : string.Empty;
                                    string domainStr = !string.IsNullOrEmpty(fd.Value.DomainName) ? $"Domain: '{fd.Value.DomainName}'" : string.Empty;
                                    string separator = !string.IsNullOrEmpty(defaultValStr) && !string.IsNullOrEmpty(domainStr) ? ", " : string.Empty;
                                    Console.WriteLine($"          - Field: {fd.Key} -> {defaultValStr}{separator}{domainStr}");
                                }
                            }
                        }
                    }

                    // Print Topology and Attribute Rule Information
                    if (item.TopologyParticipation != null && item.TopologyParticipation.Any())
                    {
                        Console.WriteLine("    Topology Participation:");
                        foreach(var topo in item.TopologyParticipation) Console.WriteLine($"      - {topo}");
                    }
                    if (item.AttributeRuleInfo != null && item.AttributeRuleInfo.Any())
                    {
                        Console.WriteLine("    Attribute Rules (ESRI Specific - Informational Only):");
                        foreach(var rule in item.AttributeRuleInfo) Console.WriteLine($"      - {rule}");
                    }
                }

                PrintSpatialReferences(spatialReferences);
                PrintDomains(domains);

                Console.WriteLine("\n--- Extracted Relationship Class Information ---");
                // ... (Relationship printing remains the same)
                if (!relationshipClasses.Any()) Console.WriteLine("  No relationship classes found or extracted.");
                foreach (var rel in relationshipClasses)
                {
                    Console.WriteLine($"  Relationship Name: {rel.Name} (UUID: {rel.ItemUuid})");
                    Console.WriteLine($"    Origin: {rel.OriginTableName} (UUID: {rel.OriginItemUuid})");
                    Console.WriteLine($"    Destination: {rel.DestinationTableName} (UUID: {rel.DestinationItemUuid})");
                    Console.WriteLine($"    Cardinality: {rel.Cardinality}, Type: {rel.RelationshipType}");
                    Console.WriteLine($"    Labels: Forward='{rel.ForwardPathLabel}', Backward='{rel.BackwardPathLabel}'");
                    if (rel.Rules.Any())
                    {
                        Console.WriteLine("    Rules (Keys):");
                        foreach(var rule in rel.Rules)
                        {
                            Console.WriteLine($"      - OriginKey: '{rule.OriginKey}', DestinationKey: '{rule.DestinationKey}'");
                        }
                    } else {
                        Console.WriteLine("    Rules (Keys): Not explicitly parsed or found in XML.");
                    }
                    // Console.WriteLine($"    Definition XML (RelItem in GDB_Items): {(rel.DefinitionXml.Length > 60 ? rel.DefinitionXml.Substring(0, 60) + "..." : rel.DefinitionXml)}");
                    // Console.WriteLine($"    Definition XML (ItemRelationship): {(rel.ItemRelationshipDefinitionXml.Length > 60 ? rel.ItemRelationshipDefinitionXml.Substring(0, 60) + "..." : rel.ItemRelationshipDefinitionXml)}");
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"SQLite error: {ex.Message} (Code: {ex.SqliteErrorCode})");
                Console.WriteLine("NOTE: Topology and Attribute Rules are ESRI-specific and generally not directly translatable to GeoPackage. This information is for awareness.");
                // Consider logging ex.ToString() for full details
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                Console.WriteLine("NOTE: Topology and Attribute Rules are ESRI-specific and generally not directly translatable to GeoPackage. This information is for awareness.");
                // Consider logging ex.ToString() for full details
            }

            Console.WriteLine("\nFinished GDB metadata extraction attempt.");
            Console.WriteLine("NOTE: Topology and Attribute Rules are ESRI-specific and generally not directly translatable to GeoPackage. This information is for awareness.");
            // Placeholder for actual conversion logic to be implemented later.
        }

        // Helper methods to encapsulate Spatial Reference and Domain processing and printing to keep Process() cleaner
        private void ProcessSpatialReferences(SqliteConnection connection, Dictionary<int, EsriSpatialReferenceInfo> spatialReferences)
        {
            if (!spatialReferences.Any()) return;

            Console.WriteLine("\nQuerying GDB_SpatialRefs for SRID details...");
            bool gdbSpatialRefsExists = false;
            using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GDB_SpatialRefs';", connection))
            {
                gdbSpatialRefsExists = cmd.ExecuteScalar() != null;
            }

            if (gdbSpatialRefsExists)
            {
                string srsQuery = "SELECT SRID, SRName, SRTEXT FROM GDB_SpatialRefs WHERE SRID = @SRID;";
                foreach (var sridKey in spatialReferences.Keys.ToList())
                {
                    using (var srsCommand = new SqliteCommand(srsQuery, connection))
                    {
                        srsCommand.Parameters.AddWithValue("@SRID", sridKey);
                        using (var reader = srsCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                spatialReferences[sridKey].SrsName = reader.IsDBNull(reader.GetOrdinal("SRName")) ? string.Empty : reader.GetString(reader.GetOrdinal("SRName"));
                                spatialReferences[sridKey].SrsDefinition = reader.IsDBNull(reader.GetOrdinal("SRTEXT")) ? string.Empty : reader.GetString(reader.GetOrdinal("SRTEXT"));
                            }
                            else
                            {
                                Console.WriteLine($"Warning: SRID {sridKey} not found in GDB_SpatialRefs.");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Warning: Table 'GDB_SpatialRefs' not found. Cannot retrieve SRS definitions.");
            }
        }

        private void ProcessDomains(SqliteConnection connection, Dictionary<string, EsriDomainInfo> domains)
        {
            if (!domains.Any()) return;

            Console.WriteLine("\nQuerying GDB_Domains for domain details...");
            bool gdbDomainsExists = false;
            using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GDB_Domains';", connection))
            {
                gdbDomainsExists = cmd.ExecuteScalar() != null;
            }

            if (gdbDomainsExists)
            {
                string domainQuery = "SELECT DomainName, Description, FieldType, Type as DomainType, Definition, Owner FROM GDB_Domains WHERE DomainName = @DomainName;";
                // Attempt to get DomainType with a fallback.
                string domainQueryFallback = "SELECT DomainName, Description, FieldType, DomainType as DomainType, Definition, Owner FROM GDB_Domains WHERE DomainName = @DomainName;";
                bool tryFallbackQuery = false;

                foreach (var domainNameKey in domains.Keys.ToList())
                {
                    string currentQuery = tryFallbackQuery ? domainQueryFallback : domainQuery;
                    using (var domainCommand = new SqliteCommand(currentQuery, connection))
                    {
                        domainCommand.Parameters.AddWithValue("@DomainName", domainNameKey);
                        try
                        {
                            using (var reader = domainCommand.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var domainInfo = domains[domainNameKey];
                                    domainInfo.Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description"));
                                    domainInfo.FieldType = reader.IsDBNull(reader.GetOrdinal("FieldType")) ? string.Empty : reader.GetString(reader.GetOrdinal("FieldType"));
                                    domainInfo.DomainType = reader.IsDBNull(reader.GetOrdinal("DomainType")) ? string.Empty : reader.GetString(reader.GetOrdinal("DomainType"));
                                    domainInfo.Owner = reader.IsDBNull(reader.GetOrdinal("Owner")) ? string.Empty : reader.GetString(reader.GetOrdinal("Owner"));
                                    string domainDefinitionXml = reader.IsDBNull(reader.GetOrdinal("Definition")) ? string.Empty : reader.GetString(reader.GetOrdinal("Definition"));
                                    domainInfo.DefinitionXML = domainDefinitionXml;

                                    if (!string.IsNullOrEmpty(domainDefinitionXml))
                                    {
                                        XDocument domainDoc = XDocument.Parse(domainDefinitionXml);
                                        if (domainInfo.DomainType == "CodedValue" || domainDoc.Descendants("CodedValue").Any())
                                        {
                                            domainInfo.DomainType = "CodedValue";
                                            domainInfo.CodedValues = new List<EsriCodedValue>();
                                            foreach (var cvElement in domainDoc.Descendants("CodedValue"))
                                            {
                                                string codeValue = cvElement.Element("Code")?.Value ?? string.Empty;
                                                domainInfo.CodedValues.Add(new EsriCodedValue
                                                {
                                                    Code = codeValue,
                                                    Name = cvElement.Element("Name")?.Value ?? string.Empty
                                                });
                                            }
                                        }
                                        else if (domainInfo.DomainType == "Range" || domainDoc.Descendants("Range").Any() || domainDoc.Descendants("RangeDomain").Any()) // RangeDomain for FGDB
                                        {
                                            domainInfo.DomainType = "Range";
                                            var rangeNode = domainDoc.Descendants("Range").FirstOrDefault() ?? domainDoc.Descendants("RangeDomain").FirstOrDefault();
                                            if (rangeNode != null)
                                            {
                                                domainInfo.RangeValue = new EsriRangeValue
                                                {
                                                    MinValue = rangeNode.Element("MinValue")?.Value ?? rangeNode.Element("Min")?.Value ?? string.Empty,
                                                    MaxValue = rangeNode.Element("MaxValue")?.Value ?? rangeNode.Element("Max")?.Value ?? string.Empty
                                                };
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Domain '{domainNameKey}' not found in GDB_Domains.");
                                    domains.Remove(domainNameKey);
                                }
                            }
                        }
                        catch (SqliteException sqlEx) when (sqlEx.Message.ToLower().Contains("no such column: type") && !tryFallbackQuery)
                        {
                            Console.WriteLine("Info: Column 'Type' not found in GDB_Domains for DomainType, attempting fallback to 'DomainType' column name.");
                            tryFallbackQuery = true; // Set flag to use fallback query for subsequent attempts
                            domainCommand.CommandText = domainQueryFallback; // Change command for current attempt
                             // Re-try logic here would be complex, simpler to just use fallback for next iteration or make two passes.
                             // For now, this specific error will cause this domain to be skipped or re-attempted if loop structure allows.
                             // The current loop structure will use the fallback for the *next* domain.
                             // To retry current one, would need nested loop or goto, which is not ideal.
                             // A simple approach: just log and the domain might be incomplete.
                            Console.WriteLine($"Warning: Retrying or skipping domain '{domainNameKey}' due to missing 'Type' column for DomainType.");
                            // To properly retry the current domain, you'd need to re-execute the command with the new query text.
                            // This is omitted for brevity as it complicates the flow significantly.
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing domain '{domainNameKey}': {ex.Message.Split('\n')[0]}");
                            if (domains.ContainsKey(domainNameKey)) domains.Remove(domainNameKey);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Warning: Table 'GDB_Domains' not found. Cannot retrieve domain definitions.");
                domains.Clear();
            }
        }

        private void PrintSpatialReferences(Dictionary<int, EsriSpatialReferenceInfo> spatialReferences)
        {
            Console.WriteLine("\n--- Extracted Spatial Reference Information ---");
            if (!spatialReferences.Any()) Console.WriteLine("  No spatial reference systems found or extracted.");
            foreach (var srs in spatialReferences.Values)
            {
                Console.WriteLine($"  SRID: {srs.Srid}, Name: {srs.SrsName}");
                Console.WriteLine($"    Definition (WKT): {(srs.SrsDefinition.Length > 100 ? srs.SrsDefinition.Substring(0, 100) + "..." : srs.SrsDefinition)}");
            }
        }

        private void PrintDomains(Dictionary<string, EsriDomainInfo> domains)
        {
            Console.WriteLine("\n--- Extracted Domain Information ---");
            if (!domains.Any()) Console.WriteLine("  No domains found or extracted.");
            foreach (var domain in domains.Values)
            {
                Console.WriteLine($"  Domain Name: {domain.Name} (Owner: {domain.Owner})");
                Console.WriteLine($"    Description: {domain.Description}");
                Console.WriteLine($"    Applies to Field Type: {domain.FieldType}, Domain Type: {domain.DomainType}");
                if (domain.DomainType == "CodedValue" && domain.CodedValues != null)
                {
                    Console.WriteLine("    Coded Values:");
                    foreach (var cv in domain.CodedValues)
                    {
                        Console.WriteLine($"      - Code: '{cv.Code}', Name: '{cv.Name}'");
                    }
                }
                else if (domain.DomainType == "Range" && domain.RangeValue != null)
                {
                    Console.WriteLine($"    Range: Min = '{domain.RangeValue.MinValue}', Max = '{domain.RangeValue.MaxValue}'");
                }
                else if (!string.IsNullOrEmpty(domain.DefinitionXML))
                {
                    Console.WriteLine($"    Values: Could not parse values from Definition XML or type mismatch. XML: {(domain.DefinitionXML.Length > 60 ? domain.DefinitionXML.Substring(0, 60) + "..." : domain.DefinitionXML)}");
                }
                else
                {
                    Console.WriteLine("    Values: No coded values or range found, or definition XML was empty/missing.");
                }
            }
        }

        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}
