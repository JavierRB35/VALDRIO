# Contextual help: Structure design
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Define and publish the structure of physical centres, analytical nodes and cost pools used by analyses. Publishing a structure is distinct from merely saving a draft.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Structure version | Selector | As applicable | Choose a draft or published version to view, copy or continue. |
| Description | Text | As applicable | Explains the scope of this structure version. |
| Centre code, name and status | Text and toggle | For each centre | Identify active physical and organisational groups. |
| Activity unit | Unit catalogue | According to node or pool | Binds the physical driver for a pool. |
| Physical centre | Selector | For each node | Connects the analytical node to a physical centre. |
| Node code and role | Text and selector | For each node | Identify cost destinations as principal or auxiliary. |
| Pool and description | Text | According to node | Identify the cost/activity grouping. |
| Functional tags | Optional metadata | No | Supplement role and unit definitions; do not replace them. |
| Save, validate, publish | Actions | By version state | Save a draft, check constraints and publish a reference structure. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Structure tree | Centres, nodes, roles and pools | Review the model topology before using it in an analysis. |
| Validation state | Model rule checks | Indicates missing requirements. |
| Published version | Persisted structure | Provides the baseline for later analyses and allocations. |

## 4. Use cases, validation and common errors
- Complete centres, principal nodes and units before publishing.
- Do not edit a published version as if it were a draft; create a new version or copy where required.
- Incorrect auxiliary roles affect secondary allocation.
- Resolve pending edits before leaving the screen.

### Centre, node and allocation role
A centre identifies a physical or organisational area. A node is an analytical cost destination within it; a centre may contain several nodes. The pool groups the node's costs and activity unit.

Choose **Principal** for a final secondary-allocation destination and **Auxiliary** for a node whose service costs are redistributed to other nodes. Role does not determine fixed or variable behaviour. Choose a comparable activity unit, such as hours; selecting it neither records quantities nor creates an allocation key.

### Deleting a centre
Deletion requires confirmation and is allowed only without nodes or historical references. Deactivate a used centre to retain its links.

## Recommended workflow
1. Create the required centres and nodes, assigning compatible pools and physical units.
2. Check codes, descriptions and relationships in the draft structure before publication.
3. Publish the consistent structure version and select it for the analysis so allocation destinations are valid.

## Optional process map (COR-05)

Under **4. Review and finalize**, the map groups nodes by economic function and role. Open an **editable version** to change it; finalized versions are read-only.

1. Click a node card in the diagram **or choose its code in the left-hand source selector**.
2. Under **Following nodes** (principal) or **Nodes served** (auxiliary), tick the destinations. Existing links are already checked. Ticking or unticking creates or removes the arrows **immediately** in the draft map.
3. A **principal** may link to **other principals** through operational sequences, but not to itself or through a production route cycle. An **auxiliary** may provide services to principals, other auxiliaries, or **itself**, regardless of economic function. Incompatible targets are disabled with a reason.
4. **Validate and save map** stores the graph for later use. Not validating the map does not prevent ordinary VALDRIO calculations, and drawing a line never publishes an allocation key.

In **Secondary allocation**, validated auxiliary service links can supply **destinations** for a draft allocation key. The user supplies weights and reviews or discards that draft separately. A 100% self-allocation with no external economic outlet can make the reciprocal system unsolvable. Sequences between principal nodes **never** generate cost transfers.

Zoom, drag, auto-arrange and image export are still available; the old two-click connector is under **Advanced controls**.
