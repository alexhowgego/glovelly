# Glovelly MCP Tools

This file is generated from `GlovellyMcpToolCatalog`. Do not edit it by hand.

Glovelly exposes authenticated MCP tools for read-only business queries and staged gig imports. Staged-write tools create reviewable draft data only; they do not create real gigs until a user reviews and commits them in Glovelly.

## `glovelly_search_contacts`

Search Glovelly contacts by name or email. Returns possible matches without guessing.

Title: Search Contacts

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `query` (optional): string. Name or email text to search for. Leave blank to list recent contacts.

Example input:

```json
{
  "query": "string"
}
```

Input schema:

```json
{
  "properties": {
    "query": {
      "description": "Name or email text to search for. Leave blank to list recent contacts.",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "matches": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "email": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "query": {
      "type": "string"
    }
  },
  "type": "object"
}
```

## `glovelly_get_contact`

Fetch read-only contact details and related summary counts for one contact.

Title: Get Contact

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `contactId` (required): string (uuid). Contact ID returned by glovelly_search_contacts.

Example input:

```json
{
  "contactId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "contactId": {
      "description": "Contact ID returned by glovelly_search_contacts.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "contactId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "contact": {
      "properties": {
        "billingAddress": {
          "properties": {
            "city": {
              "type": "string"
            },
            "country": {
              "type": "string"
            },
            "line1": {
              "type": "string"
            },
            "line2": {
              "type": "string"
            },
            "postalCode": {
              "type": "string"
            },
            "stateOrCounty": {
              "type": "string"
            }
          },
          "type": "object"
        },
        "contactId": {
          "format": "uuid",
          "type": "string"
        },
        "email": {
          "type": "string"
        },
        "gigCount": {
          "type": "integer"
        },
        "invoiceCount": {
          "type": "integer"
        },
        "invoiceEmailSubjectPattern": {
          "type": "string"
        },
        "invoiceFilenamePattern": {
          "type": "string"
        },
        "mileageRate": {
          "description": "Mileage rate for this contact.",
          "type": "number"
        },
        "name": {
          "type": "string"
        },
        "passengerMileageRate": {
          "description": "Passenger mileage rate for this contact.",
          "type": "number"
        }
      },
      "type": "object"
    },
    "found": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```

## `glovelly_list_gigs`

List gigs by optional contact, status, date range, and invoicing state.

Title: List Gigs

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `contactId` (optional): string (uuid). Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.
- `contactQuery` (optional): string. Name or email text used to look up a contact when contactId is not known.
- `fromDate` (optional): string (date). Inclusive start date in YYYY-MM-DD format.
- `invoicingState` (optional): string = all | invoiced | uninvoiced. Whether to return invoiced gigs, uninvoiced gigs, or all gigs.
- `status` (optional): string = all | draft | confirmed | planned | completed | cancelled | canceled. Gig status filter. Use confirmed or planned for planned gigs.
- `toDate` (optional): string (date). Inclusive end date in YYYY-MM-DD format.

Example input:

```json
{
  "contactId": "00000000-0000-0000-0000-000000000000",
  "contactQuery": "string",
  "fromDate": "2026-01-31"
}
```

Input schema:

```json
{
  "properties": {
    "contactId": {
      "description": "Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.",
      "format": "uuid",
      "type": "string"
    },
    "contactQuery": {
      "description": "Name or email text used to look up a contact when contactId is not known.",
      "type": "string"
    },
    "fromDate": {
      "description": "Inclusive start date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "invoicingState": {
      "description": "Whether to return invoiced gigs, uninvoiced gigs, or all gigs.",
      "enum": [
        "all",
        "invoiced",
        "uninvoiced"
      ],
      "type": "string"
    },
    "status": {
      "description": "Gig status filter. Use confirmed or planned for planned gigs.",
      "enum": [
        "all",
        "draft",
        "confirmed",
        "planned",
        "completed",
        "cancelled",
        "canceled"
      ],
      "type": "string"
    },
    "toDate": {
      "description": "Inclusive end date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "ambiguous": {
      "type": "boolean"
    },
    "currency": {
      "type": "string"
    },
    "gigs": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "contactName": {
            "type": "string"
          },
          "currency": {
            "type": "string"
          },
          "date": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "fee": {
            "description": "Gig fee.",
            "type": "number"
          },
          "gigId": {
            "format": "uuid",
            "type": "string"
          },
          "invoiceId": {
            "format": "uuid",
            "type": "string"
          },
          "isInvoiced": {
            "type": "boolean"
          },
          "status": {
            "description": "Gig lifecycle status.",
            "enum": [
              "draft",
              "confirmed",
              "completed",
              "cancelled"
            ],
            "type": "string"
          },
          "title": {
            "type": "string"
          },
          "venue": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "matches": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "email": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "message": {
      "type": "string"
    },
    "totalFees": {
      "description": "Total fees across returned gigs.",
      "type": "number"
    }
  },
  "type": "object"
}
```

## `glovelly_get_gig`

Fetch read-only details for a single gig.

Title: Get Gig

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `gigId` (required): string (uuid). Gig ID returned by glovelly_list_gigs.

Example input:

```json
{
  "gigId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "gigId": {
      "description": "Gig ID returned by glovelly_list_gigs.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "gigId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "found": {
      "type": "boolean"
    },
    "gig": {
      "properties": {
        "contactId": {
          "format": "uuid",
          "type": "string"
        },
        "contactName": {
          "type": "string"
        },
        "currency": {
          "type": "string"
        },
        "date": {
          "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
          "format": "date",
          "type": "string"
        },
        "expenses": {
          "items": {
            "properties": {
              "amount": {
                "description": "Expense amount.",
                "type": "number"
              },
              "attachmentCount": {
                "type": "integer"
              },
              "attachmentFileNames": {
                "items": {
                  "type": "string"
                },
                "type": "array"
              },
              "currency": {
                "type": "string"
              },
              "description": {
                "type": "string"
              },
              "expenseId": {
                "format": "uuid",
                "type": "string"
              },
              "reimbursementStatus": {
                "description": "Expense reimbursement status.",
                "enum": [
                  "unreimbursed",
                  "reimbursed",
                  "notClaimable"
                ],
                "type": "string"
              },
              "sortOrder": {
                "type": "integer"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "fee": {
          "description": "Gig fee.",
          "type": "number"
        },
        "gigId": {
          "format": "uuid",
          "type": "string"
        },
        "invoice": {
          "properties": {
            "contactId": {
              "format": "uuid",
              "type": "string"
            },
            "contactName": {
              "type": "string"
            },
            "currency": {
              "type": "string"
            },
            "dueDate": {
              "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
              "format": "date",
              "type": "string"
            },
            "invoiceId": {
              "format": "uuid",
              "type": "string"
            },
            "invoiceNumber": {
              "type": "string"
            },
            "issueDate": {
              "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
              "format": "date",
              "type": "string"
            },
            "outstandingAmount": {
              "description": "Amount still outstanding on this invoice.",
              "type": "number"
            },
            "status": {
              "description": "Invoice lifecycle status.",
              "enum": [
                "draft",
                "issued",
                "paid",
                "overdue",
                "cancelled"
              ],
              "type": "string"
            },
            "total": {
              "description": "Invoice total amount.",
              "type": "number"
            }
          },
          "type": "object"
        },
        "isInvoiced": {
          "type": "boolean"
        },
        "notes": {
          "type": "string"
        },
        "passengerCount": {
          "type": "integer"
        },
        "resources": {
          "items": {
            "properties": {
              "attachments": {
                "items": {
                  "properties": {
                    "attachmentId": {
                      "format": "uuid",
                      "type": "string"
                    },
                    "contentType": {
                      "type": "string"
                    },
                    "createdAt": {
                      "description": "ISO 8601 UTC timestamp.",
                      "format": "date-time",
                      "type": "string"
                    },
                    "fileName": {
                      "type": "string"
                    },
                    "sizeBytes": {
                      "type": "integer"
                    }
                  },
                  "type": "object"
                },
                "type": "array"
              },
              "createdAt": {
                "description": "ISO 8601 UTC timestamp.",
                "format": "date-time",
                "type": "string"
              },
              "isPrimary": {
                "type": "boolean"
              },
              "notes": {
                "type": "string"
              },
              "purpose": {
                "description": "Resource purpose.",
                "enum": [
                  "setList",
                  "gigPlan",
                  "contract",
                  "travel",
                  "other"
                ],
                "type": "string"
              },
              "resourceId": {
                "format": "uuid",
                "type": "string"
              },
              "resourceType": {
                "description": "Resource type.",
                "enum": [
                  "googleSheet",
                  "googleDoc",
                  "url",
                  "email",
                  "file",
                  "other"
                ],
                "type": "string"
              },
              "title": {
                "type": "string"
              },
              "updatedAt": {
                "description": "ISO 8601 UTC timestamp.",
                "format": "date-time",
                "type": "string"
              },
              "url": {
                "type": "string"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "status": {
          "description": "Gig lifecycle status.",
          "enum": [
            "draft",
            "confirmed",
            "completed",
            "cancelled"
          ],
          "type": "string"
        },
        "title": {
          "type": "string"
        },
        "travelMiles": {
          "type": "number"
        },
        "venue": {
          "type": "string"
        },
        "wasDriving": {
          "type": "boolean"
        }
      },
      "type": "object"
    }
  },
  "type": "object"
}
```

## `glovelly_list_uninvoiced_gigs`

List visible gigs that are not linked to an invoice.

Title: List Uninvoiced Gigs

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `contactId` (optional): string (uuid). Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.
- `contactQuery` (optional): string. Name or email text used to look up a contact when contactId is not known.
- `fromDate` (optional): string (date). Inclusive start date in YYYY-MM-DD format.
- `status` (optional): string = all | draft | confirmed | planned | completed | cancelled | canceled. Gig status filter. Use confirmed or planned for planned gigs.
- `toDate` (optional): string (date). Inclusive end date in YYYY-MM-DD format.

Example input:

```json
{
  "contactId": "00000000-0000-0000-0000-000000000000",
  "contactQuery": "string",
  "fromDate": "2026-01-31"
}
```

Input schema:

```json
{
  "properties": {
    "contactId": {
      "description": "Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.",
      "format": "uuid",
      "type": "string"
    },
    "contactQuery": {
      "description": "Name or email text used to look up a contact when contactId is not known.",
      "type": "string"
    },
    "fromDate": {
      "description": "Inclusive start date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "status": {
      "description": "Gig status filter. Use confirmed or planned for planned gigs.",
      "enum": [
        "all",
        "draft",
        "confirmed",
        "planned",
        "completed",
        "cancelled",
        "canceled"
      ],
      "type": "string"
    },
    "toDate": {
      "description": "Inclusive end date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "ambiguous": {
      "type": "boolean"
    },
    "currency": {
      "type": "string"
    },
    "gigs": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "contactName": {
            "type": "string"
          },
          "currency": {
            "type": "string"
          },
          "date": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "fee": {
            "description": "Gig fee.",
            "type": "number"
          },
          "gigId": {
            "format": "uuid",
            "type": "string"
          },
          "invoiceId": {
            "format": "uuid",
            "type": "string"
          },
          "isInvoiced": {
            "type": "boolean"
          },
          "status": {
            "description": "Gig lifecycle status.",
            "enum": [
              "draft",
              "confirmed",
              "completed",
              "cancelled"
            ],
            "type": "string"
          },
          "title": {
            "type": "string"
          },
          "venue": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "matches": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "email": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "message": {
      "type": "string"
    },
    "totalFees": {
      "description": "Total fees across returned gigs.",
      "type": "number"
    }
  },
  "type": "object"
}
```

## `glovelly_list_gig_resources`

List read-only metadata for links and files attached to a gig. File contents are not returned.

Title: List Gig Resources

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `gigId` (required): string (uuid). Gig ID returned by glovelly_list_gigs.

Example input:

```json
{
  "gigId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "gigId": {
      "description": "Gig ID returned by glovelly_list_gigs.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "gigId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "found": {
      "type": "boolean"
    },
    "resources": {
      "items": {
        "properties": {
          "attachments": {
            "items": {
              "properties": {
                "attachmentId": {
                  "format": "uuid",
                  "type": "string"
                },
                "contentType": {
                  "type": "string"
                },
                "createdAt": {
                  "description": "ISO 8601 UTC timestamp.",
                  "format": "date-time",
                  "type": "string"
                },
                "fileName": {
                  "type": "string"
                },
                "sizeBytes": {
                  "type": "integer"
                }
              },
              "type": "object"
            },
            "type": "array"
          },
          "createdAt": {
            "description": "ISO 8601 UTC timestamp.",
            "format": "date-time",
            "type": "string"
          },
          "isPrimary": {
            "type": "boolean"
          },
          "notes": {
            "type": "string"
          },
          "purpose": {
            "description": "Resource purpose.",
            "enum": [
              "setList",
              "gigPlan",
              "contract",
              "travel",
              "other"
            ],
            "type": "string"
          },
          "resourceId": {
            "format": "uuid",
            "type": "string"
          },
          "resourceType": {
            "description": "Resource type.",
            "enum": [
              "googleSheet",
              "googleDoc",
              "url",
              "email",
              "file",
              "other"
            ],
            "type": "string"
          },
          "title": {
            "type": "string"
          },
          "updatedAt": {
            "description": "ISO 8601 UTC timestamp.",
            "format": "date-time",
            "type": "string"
          },
          "url": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    }
  },
  "type": "object"
}
```

## `glovelly_get_gig_setlist`

Fetch the active setlist import already stored for a gig without reading Google Sheets.

Title: Get Gig Setlist

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `gigId` (required): string (uuid). Gig ID returned by glovelly_list_gigs.

Example input:

```json
{
  "gigId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "gigId": {
      "description": "Gig ID returned by glovelly_list_gigs.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "gigId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "found": {
      "type": "boolean"
    },
    "hasActiveSetlist": {
      "type": "boolean"
    },
    "setlist": {
      "properties": {
        "gigId": {
          "format": "uuid",
          "type": "string"
        },
        "importId": {
          "format": "uuid",
          "type": "string"
        },
        "importedAtUtc": {
          "description": "ISO 8601 UTC timestamp.",
          "format": "date-time",
          "type": "string"
        },
        "items": {
          "items": {
            "properties": {
              "confidence": {
                "description": "Setlist item confidence.",
                "enum": [
                  "low",
                  "medium",
                  "high"
                ],
                "type": "string"
              },
              "include": {
                "type": "boolean"
              },
              "key": {
                "type": "string"
              },
              "kind": {
                "description": "Setlist row kind.",
                "enum": [
                  "song",
                  "separator",
                  "comment"
                ],
                "type": "string"
              },
              "notes": {
                "type": "string"
              },
              "padNumber": {
                "type": "string"
              },
              "section": {
                "type": "string"
              },
              "sortOrder": {
                "type": "integer"
              },
              "sourceRowNumber": {
                "type": "integer"
              },
              "title": {
                "type": "string"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "resourceId": {
          "format": "uuid",
          "type": "string"
        },
        "sourceUrl": {
          "type": "string"
        },
        "spreadsheetId": {
          "type": "string"
        },
        "worksheetId": {
          "type": "string"
        },
        "worksheetName": {
          "type": "string"
        }
      },
      "type": "object"
    }
  },
  "type": "object"
}
```

## `glovelly_preview_expense_statement`

Build a read-only structured expense statement preview without generating PDFs or sending it anywhere.

Title: Preview Expense Statement

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `contactId` (required): string (uuid). Contact ID for the statement client.
- `expenseIds` (optional): array. Optional expense IDs to include.
- `gigIds` (optional): array. Optional gig IDs to include.
- `includeReceiptAppendix` (optional): boolean. Accepted for parity with statement previews; no PDF appendix is generated by this tool.
- `includeReceiptAttachments` (optional): boolean. Whether receipt attachment metadata should be considered by the statement builder.
- `includeReimbursedExpenses` (optional): boolean. Whether reimbursed expenses should be included.

Example input:

```json
{
  "contactId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "contactId": {
      "description": "Contact ID for the statement client.",
      "format": "uuid",
      "type": "string"
    },
    "expenseIds": {
      "description": "Optional expense IDs to include.",
      "items": {
        "format": "uuid",
        "type": "string"
      },
      "type": "array"
    },
    "gigIds": {
      "description": "Optional gig IDs to include.",
      "items": {
        "format": "uuid",
        "type": "string"
      },
      "type": "array"
    },
    "includeReceiptAppendix": {
      "description": "Accepted for parity with statement previews; no PDF appendix is generated by this tool.",
      "type": "boolean"
    },
    "includeReceiptAttachments": {
      "description": "Whether receipt attachment metadata should be considered by the statement builder.",
      "type": "boolean"
    },
    "includeReimbursedExpenses": {
      "description": "Whether reimbursed expenses should be included.",
      "type": "boolean"
    }
  },
  "required": [
    "contactId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "created": {
      "type": "boolean"
    },
    "statement": {
      "properties": {
        "contactId": {
          "format": "uuid",
          "type": "string"
        },
        "contactName": {
          "type": "string"
        },
        "currency": {
          "type": "string"
        },
        "expenseCount": {
          "type": "integer"
        },
        "gigs": {
          "items": {
            "properties": {
              "currency": {
                "type": "string"
              },
              "date": {
                "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
                "format": "date",
                "type": "string"
              },
              "expenses": {
                "items": {
                  "properties": {
                    "amount": {
                      "description": "Expense amount.",
                      "type": "number"
                    },
                    "attachments": {
                      "items": {
                        "properties": {
                          "attachmentId": {
                            "format": "uuid",
                            "type": "string"
                          },
                          "contentType": {
                            "type": "string"
                          },
                          "createdAt": {
                            "description": "ISO 8601 UTC timestamp.",
                            "format": "date-time",
                            "type": "string"
                          },
                          "fileName": {
                            "type": "string"
                          },
                          "sizeBytes": {
                            "type": "integer"
                          }
                        },
                        "type": "object"
                      },
                      "type": "array"
                    },
                    "currency": {
                      "type": "string"
                    },
                    "description": {
                      "type": "string"
                    },
                    "expenseId": {
                      "format": "uuid",
                      "type": "string"
                    },
                    "sortOrder": {
                      "type": "integer"
                    }
                  },
                  "type": "object"
                },
                "type": "array"
              },
              "gigId": {
                "format": "uuid",
                "type": "string"
              },
              "isInvoiced": {
                "type": "boolean"
              },
              "title": {
                "type": "string"
              },
              "total": {
                "description": "Total expenses for this gig.",
                "type": "number"
              },
              "venue": {
                "type": "string"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "receiptAttachmentCount": {
          "type": "integer"
        },
        "statementDate": {
          "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
          "format": "date",
          "type": "string"
        },
        "total": {
          "description": "Expense statement total.",
          "type": "number"
        }
      },
      "type": "object"
    },
    "validationErrors": {
      "additionalProperties": {
        "items": {
          "type": "string"
        },
        "type": "array"
      },
      "type": "object"
    }
  },
  "type": "object"
}
```

## `glovelly_list_invoices`

List invoices by optional contact, status, date range, and date basis.

Title: List Invoices

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `contactId` (optional): string (uuid). Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.
- `contactQuery` (optional): string. Name or email text used to look up a contact when contactId is not known.
- `dateBasis` (optional): string = issueDate | dueDate. Whether fromDate/toDate apply to issueDate or dueDate.
- `fromDate` (optional): string (date). Inclusive start date in YYYY-MM-DD format.
- `status` (optional): string = all | outstanding | issued | paid | draft | overdue | cancelled. Invoice status filter. Use outstanding for issued or overdue invoices with a balance.
- `toDate` (optional): string (date). Inclusive end date in YYYY-MM-DD format.

Example input:

```json
{
  "contactId": "00000000-0000-0000-0000-000000000000",
  "contactQuery": "string",
  "fromDate": "2026-01-31"
}
```

Input schema:

```json
{
  "properties": {
    "contactId": {
      "description": "Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match.",
      "format": "uuid",
      "type": "string"
    },
    "contactQuery": {
      "description": "Name or email text used to look up a contact when contactId is not known.",
      "type": "string"
    },
    "dateBasis": {
      "description": "Whether fromDate/toDate apply to issueDate or dueDate.",
      "enum": [
        "issueDate",
        "dueDate"
      ],
      "type": "string"
    },
    "fromDate": {
      "description": "Inclusive start date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "status": {
      "description": "Invoice status filter. Use outstanding for issued or overdue invoices with a balance.",
      "enum": [
        "all",
        "outstanding",
        "issued",
        "paid",
        "draft",
        "overdue",
        "cancelled"
      ],
      "type": "string"
    },
    "toDate": {
      "description": "Inclusive end date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "ambiguous": {
      "type": "boolean"
    },
    "currency": {
      "type": "string"
    },
    "invoices": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "contactName": {
            "type": "string"
          },
          "currency": {
            "type": "string"
          },
          "dueDate": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "invoiceId": {
            "format": "uuid",
            "type": "string"
          },
          "invoiceNumber": {
            "type": "string"
          },
          "issueDate": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "outstandingAmount": {
            "description": "Amount still outstanding on this invoice.",
            "type": "number"
          },
          "status": {
            "description": "Invoice lifecycle status.",
            "enum": [
              "draft",
              "issued",
              "paid",
              "overdue",
              "cancelled"
            ],
            "type": "string"
          },
          "total": {
            "description": "Invoice total amount.",
            "type": "number"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "matches": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "email": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "message": {
      "type": "string"
    },
    "totalOutstanding": {
      "description": "Total outstanding amount across returned invoices.",
      "type": "number"
    }
  },
  "type": "object"
}
```

## `glovelly_get_invoice`

Fetch read-only invoice details for a single invoice.

Title: Get Invoice

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `invoiceId` (required): string (uuid). Invoice ID returned by glovelly_list_invoices.

Example input:

```json
{
  "invoiceId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "invoiceId": {
      "description": "Invoice ID returned by glovelly_list_invoices.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "invoiceId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "found": {
      "type": "boolean"
    },
    "invoice": {
      "properties": {
        "contactId": {
          "format": "uuid",
          "type": "string"
        },
        "contactName": {
          "type": "string"
        },
        "currency": {
          "type": "string"
        },
        "description": {
          "type": "string"
        },
        "dueDate": {
          "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
          "format": "date",
          "type": "string"
        },
        "invoiceId": {
          "format": "uuid",
          "type": "string"
        },
        "invoiceNumber": {
          "type": "string"
        },
        "issueDate": {
          "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
          "format": "date",
          "type": "string"
        },
        "lines": {
          "items": {
            "properties": {
              "description": {
                "type": "string"
              },
              "gigId": {
                "format": "uuid",
                "type": "string"
              },
              "invoiceLineId": {
                "format": "uuid",
                "type": "string"
              },
              "lineTotal": {
                "description": "Total amount for this invoice line.",
                "type": "number"
              },
              "quantity": {
                "type": "number"
              },
              "type": {
                "description": "Invoice line type.",
                "enum": [
                  "performanceFee",
                  "mileage",
                  "passengerMileage",
                  "parking",
                  "fuel",
                  "congestionCharge",
                  "accommodation",
                  "perDiem",
                  "miscExpense",
                  "manualAdjustment"
                ],
                "type": "string"
              },
              "unitPrice": {
                "description": "Unit price for this invoice line.",
                "type": "number"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "outstandingAmount": {
          "description": "Amount still outstanding on this invoice.",
          "type": "number"
        },
        "status": {
          "description": "Invoice lifecycle status.",
          "enum": [
            "draft",
            "issued",
            "paid",
            "overdue",
            "cancelled"
          ],
          "type": "string"
        },
        "total": {
          "description": "Invoice total amount.",
          "type": "number"
        }
      },
      "type": "object"
    }
  },
  "type": "object"
}
```

## `glovelly_list_receipts`

List read-only receipt and expense records by date range and optional status.

Title: List Receipts

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `fromDate` (optional): string (date). Inclusive start date in YYYY-MM-DD format.
- `status` (optional): string = all | matched | unmatched. Receipt matching status filter.
- `toDate` (optional): string (date). Inclusive end date in YYYY-MM-DD format.

Example input:

```json
{
  "fromDate": "2026-01-31",
  "status": "all",
  "toDate": "2026-01-31"
}
```

Input schema:

```json
{
  "properties": {
    "fromDate": {
      "description": "Inclusive start date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "status": {
      "description": "Receipt matching status filter.",
      "enum": [
        "all",
        "matched",
        "unmatched"
      ],
      "type": "string"
    },
    "toDate": {
      "description": "Inclusive end date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "currency": {
      "type": "string"
    },
    "receiptCount": {
      "type": "integer"
    },
    "receipts": {
      "items": {
        "properties": {
          "amount": {
            "description": "Receipt or expense amount.",
            "type": "number"
          },
          "attachmentCount": {
            "type": "integer"
          },
          "attachmentFileNames": {
            "items": {
              "type": "string"
            },
            "type": "array"
          },
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "contactName": {
            "type": "string"
          },
          "currency": {
            "type": "string"
          },
          "description": {
            "type": "string"
          },
          "gigId": {
            "format": "uuid",
            "type": "string"
          },
          "gigTitle": {
            "type": "string"
          },
          "receiptDate": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "receiptId": {
            "format": "uuid",
            "type": "string"
          },
          "status": {
            "description": "Receipt matching status.",
            "enum": [
              "matched",
              "unmatched"
            ],
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "totalAmount": {
      "description": "Total receipt and expense amount across returned records.",
      "type": "number"
    },
    "unmatchedReceiptCount": {
      "type": "integer"
    }
  },
  "type": "object"
}
```

## `glovelly_get_business_summary`

Summarise invoice totals, paid totals, outstanding totals, expenses, and receipt counts for a period.

Title: Get Business Summary

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `fromDate` (optional): string (date). Inclusive start date in YYYY-MM-DD format.
- `toDate` (optional): string (date). Inclusive end date in YYYY-MM-DD format.

Example input:

```json
{
  "fromDate": "2026-01-31",
  "toDate": "2026-01-31"
}
```

Input schema:

```json
{
  "properties": {
    "fromDate": {
      "description": "Inclusive start date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "toDate": {
      "description": "Inclusive end date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    }
  },
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "currency": {
      "type": "string"
    },
    "expenseTotal": {
      "description": "Expense total for the period.",
      "type": "number"
    },
    "fromDate": {
      "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "invoiceTotal": {
      "description": "Invoice total for the period.",
      "type": "number"
    },
    "outstandingTotal": {
      "description": "Outstanding invoice total for the period.",
      "type": "number"
    },
    "paidTotal": {
      "description": "Paid invoice total for the period.",
      "type": "number"
    },
    "receiptCount": {
      "type": "integer"
    },
    "toDate": {
      "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "unmatchedReceiptCount": {
      "type": "integer"
    }
  },
  "type": "object"
}
```

## `glovelly_create_gig_import_batch`

Create a staged gig import batch for later human review. This does not create real gigs.

Title: Create Gig Import Batch

Safety level: StagedWrite

Requires explicit user intent: yes

Inputs:
- `notes` (optional): string. Optional notes about the source or import assumptions.
- `sourceFingerprint` (optional): string. Optional stable source identifier to help detect duplicate imports.
- `sourceName` (required): string. Human-readable source being imported, such as an email subject or document name.

Example input:

```json
{
  "sourceName": "string"
}
```

Input schema:

```json
{
  "properties": {
    "notes": {
      "description": "Optional notes about the source or import assumptions.",
      "maxLength": 4000,
      "type": "string"
    },
    "sourceFingerprint": {
      "description": "Optional stable source identifier to help detect duplicate imports.",
      "maxLength": 200,
      "type": "string"
    },
    "sourceName": {
      "description": "Human-readable source being imported, such as an email subject or document name.",
      "maxLength": 300,
      "type": "string"
    }
  },
  "required": [
    "sourceName"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "batch": {
      "properties": {
        "batchId": {
          "format": "uuid",
          "type": "string"
        },
        "createdAtUtc": {
          "description": "ISO 8601 UTC timestamp.",
          "format": "date-time",
          "type": "string"
        },
        "draftCount": {
          "type": "integer"
        },
        "notes": {
          "type": "string"
        },
        "sourceFingerprint": {
          "type": "string"
        },
        "sourceName": {
          "type": "string"
        },
        "status": {
          "description": "Staged import batch status.",
          "enum": [
            "draft",
            "committed",
            "abandoned"
          ],
          "type": "string"
        }
      },
      "type": "object"
    },
    "created": {
      "type": "boolean"
    },
    "validationErrors": {
      "items": {
        "type": "string"
      },
      "type": "array"
    }
  },
  "type": "object"
}
```

## `glovelly_add_gig_import_draft`

Add one draft gig row to a staged import batch. Missing uncertain values are allowed.

Title: Add Gig Import Draft

Safety level: StagedWrite

Requires explicit user intent: yes

Inputs:
- `accommodationNotes` (optional): string. Accommodation details or uncertainty.
- `arrivalTime` (optional): string (time). Local time in HH:mm format.
- `batchId` (required): string (uuid). Staged gig import batch ID returned by glovelly_create_gig_import_batch.
- `clientName` (optional): string. Client or booker name as found in the source.
- `confidence` (optional): string = low | medium | high. How confident the agent is that the draft row values were interpreted correctly.
- `contactEmail` (optional): string. Contact email address as found in the source.
- `contactName` (optional): string. Contact person name as found in the source.
- `contactQuery` (optional): string. Name or email text to resolve the gig client/contact.
- `date` (optional): string (date). ISO 8601 calendar date in YYYY-MM-DD format.
- `fee` (optional): number. Proposed gig fee.
- `notes` (optional): string. General notes from the source.
- `perDiem` (optional): number. Proposed per diem amount.
- `postcode` (optional): string. Venue postcode.
- `projectName` (optional): string. Project, production, tour, or engagement name.
- `rehearsalEndTime` (optional): string (time). Local time in HH:mm format.
- `rehearsalStartTime` (optional): string (time). Local time in HH:mm format.
- `showEndTime` (optional): string (time). Local time in HH:mm format.
- `showStartTime` (optional): string (time). Local time in HH:mm format.
- `sourceReference` (optional): string. Optional reference to the source row, page, message, or attachment.
- `title` (optional): string. Gig title or role summary.
- `travelNotes` (optional): string. Travel details or uncertainty.
- `venueAddress` (optional): string. Venue address.
- `venueName` (optional): string. Venue name.
- `warnings` (optional): array

Example input:

```json
{
  "batchId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "accommodationNotes": {
      "description": "Accommodation details or uncertainty.",
      "maxLength": 4000,
      "type": "string"
    },
    "arrivalTime": {
      "description": "Local time in HH:mm format.",
      "format": "time",
      "type": "string"
    },
    "batchId": {
      "description": "Staged gig import batch ID returned by glovelly_create_gig_import_batch.",
      "format": "uuid",
      "type": "string"
    },
    "clientName": {
      "description": "Client or booker name as found in the source.",
      "maxLength": 200,
      "type": "string"
    },
    "confidence": {
      "description": "How confident the agent is that the draft row values were interpreted correctly.",
      "enum": [
        "low",
        "medium",
        "high"
      ],
      "type": "string"
    },
    "contactEmail": {
      "description": "Contact email address as found in the source.",
      "maxLength": 320,
      "type": "string"
    },
    "contactName": {
      "description": "Contact person name as found in the source.",
      "maxLength": 200,
      "type": "string"
    },
    "contactQuery": {
      "description": "Name or email text to resolve the gig client/contact.",
      "type": "string"
    },
    "date": {
      "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
      "format": "date",
      "type": "string"
    },
    "fee": {
      "description": "Proposed gig fee.",
      "minimum": 0,
      "type": "number"
    },
    "notes": {
      "description": "General notes from the source.",
      "maxLength": 4000,
      "type": "string"
    },
    "perDiem": {
      "description": "Proposed per diem amount.",
      "minimum": 0,
      "type": "number"
    },
    "postcode": {
      "description": "Venue postcode.",
      "maxLength": 20,
      "type": "string"
    },
    "projectName": {
      "description": "Project, production, tour, or engagement name.",
      "maxLength": 200,
      "type": "string"
    },
    "rehearsalEndTime": {
      "description": "Local time in HH:mm format.",
      "format": "time",
      "type": "string"
    },
    "rehearsalStartTime": {
      "description": "Local time in HH:mm format.",
      "format": "time",
      "type": "string"
    },
    "showEndTime": {
      "description": "Local time in HH:mm format.",
      "format": "time",
      "type": "string"
    },
    "showStartTime": {
      "description": "Local time in HH:mm format.",
      "format": "time",
      "type": "string"
    },
    "sourceReference": {
      "description": "Optional reference to the source row, page, message, or attachment.",
      "maxLength": 500,
      "type": "string"
    },
    "title": {
      "description": "Gig title or role summary.",
      "maxLength": 200,
      "type": "string"
    },
    "travelNotes": {
      "description": "Travel details or uncertainty.",
      "maxLength": 4000,
      "type": "string"
    },
    "venueAddress": {
      "description": "Venue address.",
      "maxLength": 1000,
      "type": "string"
    },
    "venueName": {
      "description": "Venue name.",
      "maxLength": 200,
      "type": "string"
    },
    "warnings": {
      "items": {
        "type": "string"
      },
      "type": "array"
    }
  },
  "required": [
    "batchId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "batchFound": {
      "type": "boolean"
    },
    "contactMatches": {
      "items": {
        "properties": {
          "contactId": {
            "format": "uuid",
            "type": "string"
          },
          "email": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "created": {
      "type": "boolean"
    },
    "draft": {
      "properties": {
        "accommodationNotes": {
          "type": "string"
        },
        "arrivalTime": {
          "description": "Local time in HH:mm format.",
          "format": "time",
          "type": "string"
        },
        "batchId": {
          "format": "uuid",
          "type": "string"
        },
        "clientName": {
          "type": "string"
        },
        "confidence": {
          "description": "How confident the agent is that the draft row values were interpreted correctly.",
          "enum": [
            "low",
            "medium",
            "high"
          ],
          "type": "string"
        },
        "contactEmail": {
          "type": "string"
        },
        "contactName": {
          "type": "string"
        },
        "date": {
          "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
          "format": "date",
          "type": "string"
        },
        "draftId": {
          "format": "uuid",
          "type": "string"
        },
        "fee": {
          "description": "Proposed gig fee.",
          "type": "number"
        },
        "notes": {
          "type": "string"
        },
        "perDiem": {
          "description": "Proposed per diem amount.",
          "type": "number"
        },
        "postcode": {
          "type": "string"
        },
        "projectName": {
          "type": "string"
        },
        "proposedClientId": {
          "format": "uuid",
          "type": "string"
        },
        "rehearsalEndTime": {
          "description": "Local time in HH:mm format.",
          "format": "time",
          "type": "string"
        },
        "rehearsalStartTime": {
          "description": "Local time in HH:mm format.",
          "format": "time",
          "type": "string"
        },
        "showEndTime": {
          "description": "Local time in HH:mm format.",
          "format": "time",
          "type": "string"
        },
        "showStartTime": {
          "description": "Local time in HH:mm format.",
          "format": "time",
          "type": "string"
        },
        "sourceReference": {
          "type": "string"
        },
        "status": {
          "description": "Draft review status.",
          "enum": [
            "pending",
            "accepted",
            "rejected",
            "committed"
          ],
          "type": "string"
        },
        "title": {
          "type": "string"
        },
        "travelNotes": {
          "type": "string"
        },
        "venueAddress": {
          "type": "string"
        },
        "venueName": {
          "type": "string"
        },
        "warnings": {
          "items": {
            "type": "string"
          },
          "type": "array"
        }
      },
      "type": "object"
    },
    "index": {
      "type": "integer"
    },
    "validationErrors": {
      "items": {
        "type": "string"
      },
      "type": "array"
    }
  },
  "type": "object"
}
```

## `glovelly_add_gig_import_drafts`

Add multiple draft gig rows to a staged import batch, returning per-row validation feedback.

Title: Add Gig Import Drafts

Safety level: StagedWrite

Requires explicit user intent: yes

Inputs:
- `batchId` (required): string (uuid). Staged gig import batch ID returned by glovelly_create_gig_import_batch.
- `drafts` (required): array. Draft gig rows to add to the staged import batch.

Example input:

```json
{
  "batchId": "00000000-0000-0000-0000-000000000000",
  "drafts": [
    {
      "clientName": "string",
      "contactQuery": "string",
      "title": "string"
    }
  ]
}
```

Input schema:

```json
{
  "properties": {
    "batchId": {
      "description": "Staged gig import batch ID returned by glovelly_create_gig_import_batch.",
      "format": "uuid",
      "type": "string"
    },
    "drafts": {
      "description": "Draft gig rows to add to the staged import batch.",
      "items": {
        "properties": {
          "accommodationNotes": {
            "description": "Accommodation details or uncertainty.",
            "maxLength": 4000,
            "type": "string"
          },
          "arrivalTime": {
            "description": "Local time in HH:mm format.",
            "format": "time",
            "type": "string"
          },
          "clientName": {
            "description": "Client or booker name as found in the source.",
            "maxLength": 200,
            "type": "string"
          },
          "confidence": {
            "description": "How confident the agent is that the draft row values were interpreted correctly.",
            "enum": [
              "low",
              "medium",
              "high"
            ],
            "type": "string"
          },
          "contactEmail": {
            "description": "Contact email address as found in the source.",
            "maxLength": 320,
            "type": "string"
          },
          "contactName": {
            "description": "Contact person name as found in the source.",
            "maxLength": 200,
            "type": "string"
          },
          "contactQuery": {
            "description": "Name or email text to resolve the gig client/contact.",
            "type": "string"
          },
          "date": {
            "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
            "format": "date",
            "type": "string"
          },
          "fee": {
            "description": "Proposed gig fee.",
            "minimum": 0,
            "type": "number"
          },
          "notes": {
            "description": "General notes from the source.",
            "maxLength": 4000,
            "type": "string"
          },
          "perDiem": {
            "description": "Proposed per diem amount.",
            "minimum": 0,
            "type": "number"
          },
          "postcode": {
            "description": "Venue postcode.",
            "maxLength": 20,
            "type": "string"
          },
          "projectName": {
            "description": "Project, production, tour, or engagement name.",
            "maxLength": 200,
            "type": "string"
          },
          "rehearsalEndTime": {
            "description": "Local time in HH:mm format.",
            "format": "time",
            "type": "string"
          },
          "rehearsalStartTime": {
            "description": "Local time in HH:mm format.",
            "format": "time",
            "type": "string"
          },
          "showEndTime": {
            "description": "Local time in HH:mm format.",
            "format": "time",
            "type": "string"
          },
          "showStartTime": {
            "description": "Local time in HH:mm format.",
            "format": "time",
            "type": "string"
          },
          "sourceReference": {
            "description": "Optional reference to the source row, page, message, or attachment.",
            "maxLength": 500,
            "type": "string"
          },
          "title": {
            "description": "Gig title or role summary.",
            "maxLength": 200,
            "type": "string"
          },
          "travelNotes": {
            "description": "Travel details or uncertainty.",
            "maxLength": 4000,
            "type": "string"
          },
          "venueAddress": {
            "description": "Venue address.",
            "maxLength": 1000,
            "type": "string"
          },
          "venueName": {
            "description": "Venue name.",
            "maxLength": 200,
            "type": "string"
          },
          "warnings": {
            "items": {
              "type": "string"
            },
            "type": "array"
          }
        },
        "required": [],
        "type": "object"
      },
      "type": "array"
    }
  },
  "required": [
    "batchId",
    "drafts"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "batchFound": {
      "type": "boolean"
    },
    "createdCount": {
      "type": "integer"
    },
    "results": {
      "items": {
        "properties": {
          "batchFound": {
            "type": "boolean"
          },
          "contactMatches": {
            "items": {
              "properties": {
                "contactId": {
                  "format": "uuid",
                  "type": "string"
                },
                "email": {
                  "type": "string"
                },
                "name": {
                  "type": "string"
                }
              },
              "type": "object"
            },
            "type": "array"
          },
          "created": {
            "type": "boolean"
          },
          "draft": {
            "properties": {
              "accommodationNotes": {
                "type": "string"
              },
              "arrivalTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "batchId": {
                "format": "uuid",
                "type": "string"
              },
              "clientName": {
                "type": "string"
              },
              "confidence": {
                "description": "How confident the agent is that the draft row values were interpreted correctly.",
                "enum": [
                  "low",
                  "medium",
                  "high"
                ],
                "type": "string"
              },
              "contactEmail": {
                "type": "string"
              },
              "contactName": {
                "type": "string"
              },
              "date": {
                "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
                "format": "date",
                "type": "string"
              },
              "draftId": {
                "format": "uuid",
                "type": "string"
              },
              "fee": {
                "description": "Proposed gig fee.",
                "type": "number"
              },
              "notes": {
                "type": "string"
              },
              "perDiem": {
                "description": "Proposed per diem amount.",
                "type": "number"
              },
              "postcode": {
                "type": "string"
              },
              "projectName": {
                "type": "string"
              },
              "proposedClientId": {
                "format": "uuid",
                "type": "string"
              },
              "rehearsalEndTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "rehearsalStartTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "showEndTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "showStartTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "sourceReference": {
                "type": "string"
              },
              "status": {
                "description": "Draft review status.",
                "enum": [
                  "pending",
                  "accepted",
                  "rejected",
                  "committed"
                ],
                "type": "string"
              },
              "title": {
                "type": "string"
              },
              "travelNotes": {
                "type": "string"
              },
              "venueAddress": {
                "type": "string"
              },
              "venueName": {
                "type": "string"
              },
              "warnings": {
                "items": {
                  "type": "string"
                },
                "type": "array"
              }
            },
            "type": "object"
          },
          "index": {
            "type": "integer"
          },
          "validationErrors": {
            "items": {
              "type": "string"
            },
            "type": "array"
          }
        },
        "type": "object"
      },
      "type": "array"
    },
    "submittedCount": {
      "type": "integer"
    }
  },
  "type": "object"
}
```

## `glovelly_list_gig_import_batches`

List staged gig import batches and their statuses.

Title: List Gig Import Batches

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:

None.

Example input:

```json
{}
```

Input schema:

```json
{
  "properties": {},
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "batches": {
      "items": {
        "properties": {
          "batchId": {
            "format": "uuid",
            "type": "string"
          },
          "createdAtUtc": {
            "description": "ISO 8601 UTC timestamp.",
            "format": "date-time",
            "type": "string"
          },
          "draftCount": {
            "type": "integer"
          },
          "notes": {
            "type": "string"
          },
          "sourceFingerprint": {
            "type": "string"
          },
          "sourceName": {
            "type": "string"
          },
          "status": {
            "description": "Staged import batch status.",
            "enum": [
              "draft",
              "committed",
              "abandoned"
            ],
            "type": "string"
          }
        },
        "type": "object"
      },
      "type": "array"
    }
  },
  "type": "object"
}
```

## `glovelly_get_gig_import_batch`

Fetch a staged gig import batch and its draft rows.

Title: Get Gig Import Batch

Safety level: ReadOnly

Requires explicit user intent: no

Inputs:
- `batchId` (required): string (uuid). Staged gig import batch ID returned by glovelly_list_gig_import_batches.

Example input:

```json
{
  "batchId": "00000000-0000-0000-0000-000000000000"
}
```

Input schema:

```json
{
  "properties": {
    "batchId": {
      "description": "Staged gig import batch ID returned by glovelly_list_gig_import_batches.",
      "format": "uuid",
      "type": "string"
    }
  },
  "required": [
    "batchId"
  ],
  "type": "object"
}
```

Output schema:

```json
{
  "properties": {
    "batch": {
      "properties": {
        "batchId": {
          "format": "uuid",
          "type": "string"
        },
        "createdAtUtc": {
          "description": "ISO 8601 UTC timestamp.",
          "format": "date-time",
          "type": "string"
        },
        "draftCount": {
          "type": "integer"
        },
        "drafts": {
          "items": {
            "properties": {
              "accommodationNotes": {
                "type": "string"
              },
              "arrivalTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "batchId": {
                "format": "uuid",
                "type": "string"
              },
              "clientName": {
                "type": "string"
              },
              "confidence": {
                "description": "How confident the agent is that the draft row values were interpreted correctly.",
                "enum": [
                  "low",
                  "medium",
                  "high"
                ],
                "type": "string"
              },
              "contactEmail": {
                "type": "string"
              },
              "contactName": {
                "type": "string"
              },
              "date": {
                "description": "ISO 8601 calendar date in YYYY-MM-DD format.",
                "format": "date",
                "type": "string"
              },
              "draftId": {
                "format": "uuid",
                "type": "string"
              },
              "fee": {
                "description": "Proposed gig fee.",
                "type": "number"
              },
              "notes": {
                "type": "string"
              },
              "perDiem": {
                "description": "Proposed per diem amount.",
                "type": "number"
              },
              "postcode": {
                "type": "string"
              },
              "projectName": {
                "type": "string"
              },
              "proposedClientId": {
                "format": "uuid",
                "type": "string"
              },
              "rehearsalEndTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "rehearsalStartTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "showEndTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "showStartTime": {
                "description": "Local time in HH:mm format.",
                "format": "time",
                "type": "string"
              },
              "sourceReference": {
                "type": "string"
              },
              "status": {
                "description": "Draft review status.",
                "enum": [
                  "pending",
                  "accepted",
                  "rejected",
                  "committed"
                ],
                "type": "string"
              },
              "title": {
                "type": "string"
              },
              "travelNotes": {
                "type": "string"
              },
              "venueAddress": {
                "type": "string"
              },
              "venueName": {
                "type": "string"
              },
              "warnings": {
                "items": {
                  "type": "string"
                },
                "type": "array"
              }
            },
            "type": "object"
          },
          "type": "array"
        },
        "notes": {
          "type": "string"
        },
        "sourceFingerprint": {
          "type": "string"
        },
        "sourceName": {
          "type": "string"
        },
        "status": {
          "description": "Staged import batch status.",
          "enum": [
            "draft",
            "committed",
            "abandoned"
          ],
          "type": "string"
        }
      },
      "type": "object"
    },
    "found": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```
