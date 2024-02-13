# GenerateFilterStringSQL

## Background

Sometimes we need Custom Load Methods to generate the data that drive forms, but we still want the end users to be able to use FilterInPlace mode. To do this, we have to assemble the user's FilterInPlace entries, and pass that data to the Custom Load Method. Infor does this using the vendor script `GenerateFilterStringXML`, but it creates an XML format that doesn't match the SQL format that you'd find if you just look at `ThisForm.PrimaryIDOCollection.Filter`.

**Example:**

Say you were on a form dealing with items, and you filtered the `Item` property to "MyBlueCar". 

* `ThisForm.PrimaryIDOCollection.Filter` will give: `Item = N'MyBlueCar'`
* `GenerateFilterStringXML` will give: `<FilterString><Item><Property>Item</Property><Operator>=</Operator><Value>MyBlueCar</Value><DataType>CHAR</DataType><DataLength>30</DataLength></Item></FilterString>`.
* `GenerateFilterStringSQL` will give: `Item = N'MyBlueCar'`

Additional background discussion is available on the Syteline User Network forums [here](https://sun.memberclicks.net/index.php?option=com_ccboard&view=postlist&forum=15&topic=152&Itemid=).

## How to Use

Add this as a C# global Script in the Scripts menu in Design mode, and then trigger on your form from the `StdFormFilterInPlaceExecute` event. For an example of how to do this, look at the Resources form, which uses `GenerateFilterStringXML`.
